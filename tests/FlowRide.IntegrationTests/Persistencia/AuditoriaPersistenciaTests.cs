using FlowRide.Application.Auditoria.CasosUso;
using FlowRide.Application.Auditoria.Entrada.Adaptadores;
using FlowRide.Application.Auditoria.Entrada.Dto;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Infrastructure.Auditoria;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlowRide.IntegrationTests.Persistencia;

/// <summary>Auditoría legal del Silo (ADR-0011) contra PostgreSQL real: cadena de hashes, solo INSERT y atomicidad.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class AuditoriaPersistenciaTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private EntornoAprovisionamiento _entorno = null!;

    public async Task InitializeAsync() => _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);

    public async Task DisposeAsync() => await _entorno.DisposeAsync();

    private async Task<string> SiloNuevoAsync()
    {
        var tenant = $"aud-{Guid.NewGuid():N}"[..16];
        await _entorno.AprovisionarAsync(tenant);
        return tenant;
    }

    private static EventoAuditoria Evento(string recursoId) => new(Guid.NewGuid(), AccionesAuditoria.UsuarioCrear,
        AccionesAuditoria.RecursoUsuario, recursoId, Ahora, ResultadoAuditoria.Exitoso,
        valoresDespues: new Dictionary<string, string?> { ["rol"] = "Administrador", ["activo"] = "true" });

    private async Task RegistrarAsync(string tenant, params EventoAuditoria[] eventos)
    {
        await using var silo = await _entorno.AbrirSiloAsync(tenant);
        var repositorio = new EventoAuditoriaRepositorioAdaptador(silo.Contexto);
        foreach (var evento in eventos) await repositorio.CrearAsync(evento, CancellationToken.None);
        await new UnidadTrabajoEf(silo.Contexto).GuardarCambiosAsync(CancellationToken.None);
    }

    private async Task<VerificacionAuditoriaRespuesta> VerificarAsync(string tenant)
    {
        await using var silo = await _entorno.AbrirSiloAsync(tenant);
        return await new VerificarAuditoriaAdaptadorEntrada(
                new VerificarAuditoriaCasoUso(new EventoAuditoriaRepositorioAdaptador(silo.Contexto)))
            .VerificarAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LosRolesDelSistema_LleganConLaMigracion()
    {
        var tenant = await SiloNuevoAsync();
        await using var silo = await _entorno.AbrirSiloAsync(tenant);

        var roles = await silo.Contexto.Roles.OrderBy(rol => rol.Nombre).Select(rol => rol.Id).ToListAsync();

        Assert.Equal(RolesSistema.Todos.Select(rol => rol.Id).Order(), roles.Order());
    }

    [Fact]
    public async Task EventosDeTransaccionesDistintas_FormanUnaSolaCadena()
    {
        var tenant = await SiloNuevoAsync();
        await RegistrarAsync(tenant, Evento("u-1"));
        await RegistrarAsync(tenant, Evento("u-2"), Evento("u-3"));

        await using var silo = await _entorno.AbrirSiloAsync(tenant);
        var eventos = await silo.Contexto.EventosAuditoria.OrderBy(evento => evento.Id).ToListAsync();

        Assert.Equal(["u-1", "u-2", "u-3"], eventos.Select(evento => evento.RecursoId));
        Assert.Equal(EventoAuditoria.HashInicial, eventos[0].HashPrevio);
        Assert.Equal(eventos[0].Hash, eventos[1].HashPrevio);
        Assert.Equal(eventos[1].Hash, eventos[2].HashPrevio);
        Assert.True((await VerificarAsync(tenant)).Integra);
    }

    [Fact]
    public async Task EscriturasConcurrentes_NoBifurcanLaCadena()
    {
        var tenant = await SiloNuevoAsync();

        await Task.WhenAll(Enumerable.Range(1, 12).Select(i => RegistrarAsync(tenant, Evento($"u-{i}"))));

        var verificacion = await VerificarAsync(tenant);
        Assert.True(verificacion.Integra, verificacion.Mensaje);
        Assert.Equal(12, verificacion.TotalEventos);
    }

    [Theory]
    [InlineData("UPDATE audit.evento_auditoria SET accion = 'otra'")]
    [InlineData("DELETE FROM audit.evento_auditoria")]
    [InlineData("TRUNCATE audit.evento_auditoria")]
    public async Task LaBase_SoloAdmiteInserciones(string sql)
    {
        var tenant = await SiloNuevoAsync();
        await RegistrarAsync(tenant, Evento("u-1"));

        await using var silo = await _entorno.AbrirSiloAsync(tenant);
        var error = await Assert.ThrowsAsync<PostgresException>(() => silo.Contexto.Database.ExecuteSqlRawAsync(sql));

        Assert.Contains("solo admite inserciones", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnEventoAlteradoSaltandoseElTrigger_SeDetecta()
    {
        var tenant = await SiloNuevoAsync();
        await RegistrarAsync(tenant, Evento("u-1"), Evento("u-2"), Evento("u-3"));

        // Solo un superusuario puede saltarse el trigger: simula una manipulación directa de la base.
        var baseDelSilo = new NpgsqlConnectionStringBuilder(_entorno.CadenaAdministracion) { Database = $"flowride_{tenant.Replace('-', '_')}" };
        await EntornoAprovisionamiento.EjecutarComoAdministradorAsync(baseDelSilo.ConnectionString, """
            ALTER TABLE audit.evento_auditoria DISABLE TRIGGER USER;
            UPDATE audit.evento_auditoria SET valores_despues = '{"rol": "Pasajero", "activo": "true"}' WHERE recurso_id = 'u-2';
            ALTER TABLE audit.evento_auditoria ENABLE TRIGGER USER;
            """);

        var verificacion = await VerificarAsync(tenant);

        Assert.False(verificacion.Integra);
        Assert.Equal(3, verificacion.TotalEventos);
        Assert.Equal(2, verificacion.PrimerEventoAlteradoId);
    }

    [Fact]
    public async Task SiElCambioFalla_SuEventoDeAuditoriaTampocoSeGuarda()
    {
        var tenant = await SiloNuevoAsync();
        var telefono = $"+57{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}";
        await using (var silo = await _entorno.AbrirSiloAsync(tenant))
        {
            silo.Contexto.Usuarios.Add(new Usuario(Guid.NewGuid(), "Ana", telefono, RolesSistema.AdministradorId, Ahora));
            await silo.Contexto.SaveChangesAsync();
        }

        await using (var silo = await _entorno.AbrirSiloAsync(tenant))
        {
            // Mismo teléfono: viola el índice único al confirmar, junto con el evento que la registraba.
            silo.Contexto.Usuarios.Add(new Usuario(Guid.NewGuid(), "Beto", telefono, RolesSistema.AdministradorId, Ahora));
            await new EventoAuditoriaRepositorioAdaptador(silo.Contexto).CrearAsync(Evento("beto"), CancellationToken.None);
            await Assert.ThrowsAsync<DbUpdateException>(() => new UnidadTrabajoEf(silo.Contexto).GuardarCambiosAsync(CancellationToken.None));
        }

        Assert.Equal(0, (await VerificarAsync(tenant)).TotalEventos);
    }
}
