using FlowRide.Application.Auditoria.CasosUso;
using FlowRide.Application.Auditoria.Entrada.Adaptadores;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.CasosUso;
using FlowRide.Application.Identidad.Entrada.Adaptadores;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Domain.Auditoria;

namespace FlowRide.UnitTests.Application.Identidad;

/// <summary>Los adaptadores de entrada validan la petición, la convierten en datos del dominio y arman la respuesta.</summary>
public class AdaptadoresEntradaIdentidadTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class CasoUsoGrabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder)
        : ICasoUsoConRetorno<TEntrada, TSalida>
    {
        public List<TEntrada> Recibidos { get; } = [];

        public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
        {
            Recibidos.Add(datos);
            return Task.FromResult(responder(datos));
        }
    }

    private static Usuario Usuario(string rol = "Administrador", string correo = "ana@a.co") =>
        new(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PorNombre(rol)!.Id, Ahora, correo, "sub-1");

    private static readonly CrearUsuarioPeticion Peticion = new()
    {
        Nombre = " Ana ",
        Correo = " ana@a.co ",
        TelefonoWhatsApp = " +57 300 111-2233 ",
        Rol = " OperadorLogistico "
    };

    [Fact]
    public async Task Crear_RecortaConvierteYArmaLaRespuesta()
    {
        var casoUso = new CasoUsoGrabador<CrearUsuarioDominio, Usuario>(datos => Usuario(datos.Rol));

        var respuesta = await new CrearUsuarioAdaptadorEntrada(casoUso).CrearAsync(Peticion, Actor, CancellationToken.None);

        Assert.Equal(new CrearUsuarioDominio("Ana", "ana@a.co", "+573001112233", "OperadorLogistico", Actor), Assert.Single(casoUso.Recibidos));
        Assert.Equal("OperadorLogistico", respuesta.Rol);
        Assert.True(respuesta.Activo);
        Assert.Equal(Ahora, respuesta.CreadoEn);
    }

    [Theory]
    [InlineData(null, "a@a.co", "+57", "Administrador")]
    [InlineData("Ana", " ", "+57", "Administrador")]
    [InlineData("Ana", "a@a.co", null, "Administrador")]
    [InlineData("Ana", "a@a.co", "+57", "")]
    public async Task Crear_CamposObligatorios(string? nombre, string? correo, string? telefono, string? rol)
    {
        var casoUso = new CasoUsoGrabador<CrearUsuarioDominio, Usuario>(_ => throw new InvalidOperationException());
        var peticion = new CrearUsuarioPeticion { Nombre = nombre, Correo = correo, TelefonoWhatsApp = telefono, Rol = rol };

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new CrearUsuarioAdaptadorEntrada(casoUso).CrearAsync(peticion, Actor, CancellationToken.None));

        Assert.Contains("rol son obligatorios", error.Message, StringComparison.Ordinal);
        Assert.Empty(casoUso.Recibidos);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new CrearUsuarioAdaptadorEntrada(casoUso).CrearAsync(null!, Actor, CancellationToken.None));
    }

    [Theory]
    [InlineData(true, "Administrador creado")]
    [InlineData(false, "ya existía")]
    public async Task AdministradorInicial_IgnoraElRolYExplicaElResultado(bool nuevo, string mensaje)
    {
        var casoUso = new CasoUsoGrabador<CrearUsuarioDominio, ResultadoAdministradorInicial>(_ =>
            new ResultadoAdministradorInicial(Usuario(), nuevo));

        var respuesta = await new AsegurarAdministradorInicialAdaptadorEntrada(casoUso)
            .AsegurarAsync(Peticion with { Rol = null }, CancellationToken.None);

        var datos = Assert.Single(casoUso.Recibidos);
        Assert.Equal(RolesSistema.Administrador, datos.Rol);
        Assert.Null(datos.ActorId);
        Assert.Equal(nuevo, respuesta.Nuevo);
        Assert.Contains(mensaje, respuesta.Mensaje, StringComparison.Ordinal);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new AsegurarAdministradorInicialAdaptadorEntrada(casoUso).AsegurarAsync(new CrearUsuarioPeticion(), CancellationToken.None));
    }

    [Fact]
    public async Task CambiarEstadoYRol_PasanElActor()
    {
        var id = Guid.NewGuid();
        var estado = new CasoUsoGrabador<CambiarEstadoUsuarioDominio, Usuario>(_ => Usuario());
        var rol = new CasoUsoGrabador<CambiarRolUsuarioDominio, Usuario>(_ => Usuario());

        await new CambiarEstadoUsuarioAdaptadorEntrada(estado).CambiarEstadoAsync(id, false, Actor, CancellationToken.None);
        await new CambiarRolUsuarioAdaptadorEntrada(rol)
            .CambiarRolAsync(id, new CambiarRolUsuarioPeticion { Rol = " Administrador " }, Actor, CancellationToken.None);

        Assert.Equal(new CambiarEstadoUsuarioDominio(id, false, Actor), Assert.Single(estado.Recibidos));
        Assert.Equal(new CambiarRolUsuarioDominio(id, "Administrador", Actor), Assert.Single(rol.Recibidos));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new CambiarRolUsuarioAdaptadorEntrada(rol)
            .CambiarRolAsync(id, new CambiarRolUsuarioPeticion(), Actor, CancellationToken.None));
    }

    [Fact]
    public async Task Consultar_OrdenaPorNombre()
    {
        var casoUso = new CasoUsoGrabador<ConsultarUsuariosDominio, IReadOnlyList<Usuario>>(_ =>
        [
            new Usuario(Guid.NewGuid(), "Zoe", "+1", RolesSistema.AdministradorId, Ahora),
            new Usuario(Guid.NewGuid(), "Ana", "+2", RolesSistema.ConductorId, Ahora)
        ]);

        var respuesta = await new ConsultarUsuariosAdaptadorEntrada(casoUso).ConsultarAsync(" Conductor ", CancellationToken.None);

        Assert.Equal(["Ana", "Zoe"], respuesta.Select(usuario => usuario.Nombre));
        Assert.Equal("Conductor", Assert.Single(casoUso.Recibidos).Rol);
    }

    [Fact]
    public async Task Resolver_ConvierteElUsuarioONull()
    {
        var usuario = Usuario("OperadorLogistico");
        var casoUso = new CasoUsoGrabador<ResolverIdentidadDominio, Usuario?>(datos => datos.IdentidadExternaId == "sub-1" ? usuario : null);
        var adaptador = new ResolverIdentidadAdaptadorEntrada(casoUso);

        Assert.Equal(new UsuarioAutenticadoRespuesta(usuario.Id, "Ana", "ana@a.co", "OperadorLogistico"),
            await adaptador.ResolverAsync("sub-1", CancellationToken.None));
        Assert.Null(await adaptador.ResolverAsync("sub-2", CancellationToken.None));
    }

    [Fact]
    public async Task VerificarAuditoria_IntegraORota()
    {
        var auditoria = new AuditoriaEnMemoria();
        var adaptador = new VerificarAuditoriaAdaptadorEntrada(new VerificarAuditoriaCasoUso(auditoria));
        var evento = EventoAuditoriaTests.Evento();
        evento.Sellar(EventoAuditoria.HashInicial);
        EventoAuditoriaTests.Asignar(evento, nameof(EventoAuditoria.Id), 7L);
        auditoria.Eventos.Add(evento);

        var integra = await adaptador.VerificarAsync(CancellationToken.None);
        EventoAuditoriaTests.Asignar(evento, nameof(EventoAuditoria.RecursoId), "otro");
        var rota = await adaptador.VerificarAsync(CancellationToken.None);

        Assert.True(integra.Integra);
        Assert.Equal(1, integra.TotalEventos);
        Assert.Null(integra.PrimerEventoAlteradoId);
        Assert.False(rota.Integra);
        Assert.Equal(7, rota.PrimerEventoAlteradoId);
        Assert.Contains("evento 7", rota.Mensaje, StringComparison.Ordinal);
    }
}
