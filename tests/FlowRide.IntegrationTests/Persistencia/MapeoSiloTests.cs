using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlowRide.IntegrationTests.Persistencia;

/// <summary>
/// Mapeo de EF Core del Silo contra PostgreSQL real (ADR-0004): los agregados se guardan y se reconstruyen,
/// y la base hace cumplir las restricciones de consistencia de ADR-0007.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class MapeoSiloTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Tenant = "mapeo";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 7, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateOnly Vigente = new(2027, 12, 31);

    private EntornoAprovisionamiento _entorno = null!;

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await _entorno.AprovisionarAsync(Tenant);
    }

    public async Task DisposeAsync() => await _entorno.DisposeAsync();

    [Fact]
    public async Task Solicitud_SeReconstruyeConSuHistorialYCancelacion_SinDuplicarElHistorial()
    {
        var datos = await SembrarAsync();
        var solicitud = datos.Pasajero.SolicitarViaje(Guid.NewGuid(), "Hotel", "Aeropuerto", Ahora.AddHours(2),
            TimeSpan.FromMinutes(45), 2, CanalSolicitud.WhatsApp, Ahora, urgente: true);
        solicitud.Cancelar(Guid.NewGuid(), "Vuelo reprogramado", datos.Pasajero.Id, Ahora.AddMinutes(5));
        await GuardarAsync(solicitud);

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        var leida = await silo.Contexto.Solicitudes
            .Include(s => s.Historial)
            .Include(s => s.Cancelacion)
            .SingleAsync(s => s.Id == solicitud.Id);

        Assert.Equal(EstadoSolicitud.Cancelada, leida.Estado);
        Assert.Equal(2, leida.Historial.Count);
        Assert.Equal("Vuelo reprogramado", leida.Cancelacion!.Motivo);
        Assert.Equal(Ahora.AddHours(2), leida.FechaHoraServicio); // mismo instante, guardado en UTC
        Assert.Equal(TimeSpan.Zero, leida.FechaHoraServicio.Offset);
        Assert.Equal(TimeSpan.FromMinutes(45), leida.DuracionEstimada);
        Assert.True(leida.Urgente);
    }

    [Fact]
    public async Task Usuarios_SeLeenComoSuTipoConcreto()
    {
        var datos = await SembrarAsync();

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        var conductor = await silo.Contexto.Usuarios.SingleAsync(u => u.Id == datos.Conductor.Id);
        var pasajero = await silo.Contexto.Usuarios.SingleAsync(u => u.Id == datos.Pasajero.Id);

        var leido = Assert.IsType<Conductor>(conductor);
        Assert.Equal(datos.Conductor.NumeroLicencia, leido.NumeroLicencia);
        Assert.Equal(datos.Pasajero.CodigoInterno, Assert.IsType<Pasajero>(pasajero).CodigoInterno);
    }

    [Fact]
    public async Task Conductor_SeReconstruyeConSusTurnosYDocumentos()
    {
        var datos = await SembrarAsync();

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        var conductor = await silo.Contexto.Conductores
            .Include(c => c.Turnos)
            .Include(c => c.Documentos)
            .SingleAsync(c => c.Id == datos.Conductor.Id);

        var turno = Assert.Single(conductor.Turnos);
        Assert.Equal(datos.Turno.Franja, turno.Franja);
        Assert.Equal(TipoDocumento.Licencia, Assert.Single(conductor.Documentos).Tipo);
    }

    [Fact]
    public async Task TurnosVigentesSolapados_LaBaseLosRechaza()
    {
        var datos = await SembrarAsync();

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        // Se salta la validación del agregado para comprobar la última línea de defensa: la restricción EXCLUDE.
        silo.Contexto.Add(new Turno(Guid.NewGuid(), datos.Conductor.Id, datos.Turno.Inicio.AddHours(1), datos.Turno.Fin.AddHours(1)));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => silo.Contexto.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task DosAsignacionesVigentesParaLaMismaSolicitud_LaBaseLasRechaza()
    {
        var datos = await SembrarAsync();
        var otro = await SembrarAsync();
        var solicitud = await CrearSolicitudAsync(datos);

        await GuardarAsync(new Asignacion(Guid.NewGuid(), solicitud.Id, datos.Conductor.Id, datos.Vehiculo.Id,
            solicitud.Franja(), OrigenAsignacion.Manual, Ahora));

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        silo.Contexto.Add(new Asignacion(Guid.NewGuid(), solicitud.Id, otro.Conductor.Id, otro.Vehiculo.Id,
            solicitud.Franja(), OrigenAsignacion.Manual, Ahora));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => silo.Contexto.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task ConductorConDosAsignacionesVigentesSolapadas_LaBaseLoRechaza()
    {
        var datos = await SembrarAsync();
        var otro = await SembrarAsync();
        var primera = await CrearSolicitudAsync(datos);
        var segunda = await CrearSolicitudAsync(datos);

        await GuardarAsync(new Asignacion(Guid.NewGuid(), primera.Id, datos.Conductor.Id, datos.Vehiculo.Id,
            primera.Franja(), OrigenAsignacion.Manual, Ahora));

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        silo.Contexto.Add(new Asignacion(Guid.NewGuid(), segunda.Id, datos.Conductor.Id, otro.Vehiculo.Id,
            segunda.Franja(), OrigenAsignacion.Manual, Ahora));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => silo.Contexto.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task EscriturasConcurrentes_SoloConfirmaLaPrimera()
    {
        var datos = await SembrarAsync();
        var solicitud = await CrearSolicitudAsync(datos);

        await using var primero = await _entorno.AbrirSiloAsync(Tenant);
        await using var segundo = await _entorno.AbrirSiloAsync(Tenant);
        var copiaUno = await primero.Contexto.Solicitudes.SingleAsync(s => s.Id == solicitud.Id);
        var copiaDos = await segundo.Contexto.Solicitudes.SingleAsync(s => s.Id == solicitud.Id);

        copiaUno.MarcarSinCandidatos("Sin conductores en turno", Ahora);
        await primero.Contexto.SaveChangesAsync();

        copiaDos.MarcarSinCandidatos("Sin conductores en turno", Ahora);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.Contexto.SaveChangesAsync());
    }

    [Fact]
    public async Task Viaje_SeReconstruyeConParadasEIncidentes_YAdmiteCalificacion()
    {
        var datos = await SembrarAsync();
        var solicitud = await CrearSolicitudAsync(datos);
        var asignacion = new Asignacion(Guid.NewGuid(), solicitud.Id, datos.Conductor.Id, datos.Vehiculo.Id,
            solicitud.Franja(), OrigenAsignacion.Manual, Ahora);
        datos.Conductor.AceptarAsignacion(asignacion);
        var viaje = asignacion.Iniciar(Guid.NewGuid(), solicitud.FechaHoraServicio);
        viaje.RegistrarParada(Guid.NewGuid(), "Recogida", 1, solicitud.FechaHoraServicio);
        viaje.RegistrarIncidente(Guid.NewGuid(), TipoIncidente.Retraso, "Trancón", datos.Conductor.Id, solicitud.FechaHoraServicio);
        viaje.Finalizar(solicitud.FechaHoraServicio.AddMinutes(50));
        var calificacion = datos.Pasajero.Calificar(Guid.NewGuid(), viaje, 5, "Puntual", Ahora.AddHours(4));
        await GuardarAsync(asignacion, viaje, calificacion);

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        var leido = await silo.Contexto.Viajes
            .Include(v => v.Paradas)
            .Include(v => v.Incidentes)
            .SingleAsync(v => v.Id == viaje.Id);

        Assert.Equal(EstadoViaje.Finalizado, leido.Estado);
        Assert.Single(leido.Paradas);
        Assert.Equal(TipoIncidente.Retraso, Assert.Single(leido.Incidentes).Tipo);
        Assert.Equal(5, (await silo.Contexto.Calificaciones.SingleAsync(c => c.ViajeId == viaje.Id)).Puntaje);
        Assert.Equal(EstadoAsignacion.EnCurso, (await silo.Contexto.Asignaciones.SingleAsync(a => a.Id == asignacion.Id)).Estado);
    }

    private sealed record Datos(Pasajero Pasajero, Conductor Conductor, Turno Turno, Vehiculo Vehiculo);

    /// <summary>Datos mínimos con valores únicos: las pruebas comparten la misma base del Silo.</summary>
    private async Task<Datos> SembrarAsync()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var rol = new Rol(Guid.NewGuid(), $"Rol-{sufijo}", "Rol de prueba");
        var empresa = new EmpresaCliente(Guid.NewGuid(), "Aerolínea", $"nit-{sufijo}", "ops@aero.co");
        var pasajero = new Pasajero(Guid.NewGuid(), "Ana", $"+57300{sufijo}", rol.Id, Ahora, $"P-{sufijo}", empresa.Id);
        var conductor = new Conductor(Guid.NewGuid(), "Luis", $"+57310{sufijo}", rol.Id, Ahora, $"L-{sufijo}", "C2", Vigente);
        var turno = conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Ahora, Ahora.AddHours(10)));
        conductor.AgregarDocumento(Guid.NewGuid(), TipoDocumento.Licencia, $"documents/{sufijo}.pdf", Vigente);
        var vehiculo = new Vehiculo(Guid.NewGuid(), sufijo[..6], "Toyota", "Hiace", 10, Vigente, Vigente);

        await GuardarAsync(rol, empresa, pasajero, conductor, vehiculo);
        return new Datos(pasajero, conductor, turno, vehiculo);
    }

    private async Task<SolicitudViaje> CrearSolicitudAsync(Datos datos)
    {
        var solicitud = datos.Pasajero.SolicitarViaje(Guid.NewGuid(), "Hotel", "Aeropuerto", Ahora.AddHours(2),
            TimeSpan.FromMinutes(45), 2, CanalSolicitud.WhatsApp, Ahora);
        await GuardarAsync(solicitud);
        return solicitud;
    }

    private async Task GuardarAsync(params object[] entidades)
    {
        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        silo.Contexto.AddRange(entidades);
        await silo.Contexto.SaveChangesAsync();
    }
}
