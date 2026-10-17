using FlowRide.Application.Identidad.CasosUso;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Identidad;

public abstract class CasosUsoIdentidadBase
{
    protected static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    internal UsuariosEnMemoria Usuarios { get; } = new();
    internal AuditoriaEnMemoria Auditoria { get; } = new();
    internal ProveedorEnMemoria Proveedor { get; } = new();
    internal UnidadTrabajoEnMemoria UnidadTrabajo { get; }

    protected CasosUsoIdentidadBase() => UnidadTrabajo = new UnidadTrabajoEnMemoria(Auditoria);

    internal CrearUsuarioCasoUso Crear() =>
        new(new UsuarioDominio(Usuarios), Usuarios, Auditoria, UnidadTrabajo, Proveedor, new RelojFijo(Ahora));

    internal static IReadOnlyDictionary<string, string?> Valores(string? json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(json!)!;
}

public class CrearUsuarioCasoUsoTests : CasosUsoIdentidadBase
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly CrearUsuarioDominio Datos = new("Ana Pérez", "ana@a.co", "+57300", "OperadorLogistico", Actor);

    [Fact]
    public async Task CreaCuentaUsuarioYAuditoria_YEnviaElCorreoDeContrasena()
    {
        var usuario = await Crear().EjecutarAsync(Datos, CancellationToken.None);

        Assert.Equal(RolesSistema.OperadorLogisticoId, usuario.RolId);
        Assert.Equal("sub-ana@a.co", usuario.IdentidadExternaId);
        Assert.Equal(Ahora, usuario.CreadoEn);
        Assert.True(usuario.Activo);
        Assert.Same(usuario, Usuarios.Usuarios[usuario.Id]);
        Assert.Equal(["sub-ana@a.co"], Proveedor.CorreosEnviados);
        Assert.Equal(1, UnidadTrabajo.Confirmaciones);

        var evento = Assert.Single(Auditoria.Eventos);
        Assert.Equal(AccionesAuditoria.UsuarioCrear, evento.Accion);
        Assert.Equal(Actor, evento.ActorId);
        Assert.Equal(usuario.Id.ToString(), evento.RecursoId);
        Assert.True(evento.HashValido());
        Assert.Equal("OperadorLogistico", Valores(evento.ValoresDespues)["rol"]);
        Assert.DoesNotContain("ana@a.co", evento.ValoresDespues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CuentaQueYaExistiaEnElProveedor_SeReutiliza()
    {
        Proveedor.Cuentas["ana@a.co"] = ("sub-previo", "Ana");

        var usuario = await Crear().EjecutarAsync(Datos, CancellationToken.None);

        Assert.Equal("sub-previo", usuario.IdentidadExternaId);
    }

    [Theory]
    [InlineData("Conductor", "ana@a.co", "+57300")]
    [InlineData("Administrador", "ana", "+57300")]
    [InlineData("Administrador", "existente@a.co", "+57300")]
    [InlineData("Administrador", "ana@a.co", "+57999")]
    public async Task ReglasIncumplidas_NoTocanElProveedorNiLaBase(string rol, string correo, string telefono)
    {
        Usuarios.Registrar("Existente", "existente@a.co", "+57999");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Crear().EjecutarAsync(Datos with { Rol = rol, Correo = correo, TelefonoWhatsApp = telefono }, CancellationToken.None));

        Assert.Equal(0, Proveedor.Llamadas);
        Assert.Equal(0, Usuarios.Creados);
        Assert.Empty(Auditoria.Eventos);
    }

    [Fact]
    public async Task FalloAlGuardar_NoEnviaElCorreo()
    {
        UnidadTrabajo.Falla = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Crear().EjecutarAsync(Datos, CancellationToken.None));

        Assert.Empty(Proveedor.CorreosEnviados);
    }

    [Fact]
    public async Task DatosNulos_SeRechazan() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => Crear().EjecutarAsync(null!, CancellationToken.None));
}

public class AsegurarAdministradorInicialCasoUsoTests : CasosUsoIdentidadBase
{
    private static readonly CrearUsuarioDominio Datos = new("Admin", "admin@a.co", "+57300", "OperadorLogistico", Guid.NewGuid());

    private AsegurarAdministradorInicialCasoUso CasoUso() => new(Usuarios, Crear());

    [Fact]
    public async Task SinAdministrador_LoCreaComoAdministradorSinActor()
    {
        var resultado = await CasoUso().EjecutarAsync(Datos, CancellationToken.None);

        Assert.True(resultado.Nuevo);
        Assert.Equal(RolesSistema.AdministradorId, resultado.Usuario.RolId);
        Assert.Null(Assert.Single(Auditoria.Eventos).ActorId);
    }

    [Fact]
    public async Task AdministradorExistente_NoHaceNada()
    {
        var existente = Usuarios.Registrar("Admin", "admin@a.co", "+57300");

        var resultado = await CasoUso().EjecutarAsync(Datos, CancellationToken.None);

        Assert.False(resultado.Nuevo);
        Assert.Same(existente, resultado.Usuario);
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Fact]
    public async Task CorreoDeUnUsuarioConOtroRol_SeRechaza()
    {
        Usuarios.Registrar("Admin", "admin@a.co", "+57300", RolesSistema.OperadorLogisticoId);

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() => CasoUso().EjecutarAsync(Datos, CancellationToken.None));

        Assert.Contains("OperadorLogistico", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatosNulos_SeRechazan() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CasoUso().EjecutarAsync(null!, CancellationToken.None));
}

public class CambiarEstadoUsuarioCasoUsoTests : CasosUsoIdentidadBase
{
    private static readonly Guid Actor = Guid.NewGuid();

    private CambiarEstadoUsuarioCasoUso CasoUso() =>
        new(new UsuarioDominio(Usuarios), Usuarios, Auditoria, UnidadTrabajo, Proveedor, new RelojFijo(Ahora));

    [Fact]
    public async Task Desactivar_RevocaElAccesoAuditaYDeshabilitaLaCuenta()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300");

        var usuario = await CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(ana.Id, false, Actor), CancellationToken.None);

        Assert.False(usuario.Activo);
        Assert.False(Proveedor.Habilitadas["sub-ana@a.co"]);
        var evento = Assert.Single(Auditoria.Eventos);
        Assert.Equal(AccionesAuditoria.UsuarioDesactivar, evento.Accion);
        Assert.Equal("true", Valores(evento.ValoresAntes)["activo"]);
        Assert.Equal("false", Valores(evento.ValoresDespues)["activo"]);
    }

    [Fact]
    public async Task Activar_HabilitaLaCuenta()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300", activo: false);

        await CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(ana.Id, true, Actor), CancellationToken.None);

        Assert.True(ana.Activo);
        Assert.True(Proveedor.Habilitadas["sub-ana@a.co"]);
        Assert.Equal(AccionesAuditoria.UsuarioActivar, Assert.Single(Auditoria.Eventos).Accion);
    }

    [Fact]
    public async Task MismoEstado_NoAuditaPeroSincronizaLaCuenta()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300", activo: false);
        Proveedor.Habilitadas["sub-ana@a.co"] = true;

        await CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(ana.Id, false, Actor), CancellationToken.None);

        Assert.Empty(Auditoria.Eventos);
        Assert.Equal(0, UnidadTrabajo.Confirmaciones);
        Assert.False(Proveedor.Habilitadas["sub-ana@a.co"]);
    }

    [Fact]
    public async Task UsuarioSinCuentaDelPanel_SoloCambiaElEstadoInterno()
    {
        var conductor = Usuarios.Registrar("Carlos", null, "+57302", RolesSistema.ConductorId);

        await CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(conductor.Id, false, Actor), CancellationToken.None);

        Assert.False(conductor.Activo);
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Fact]
    public async Task UnoMismo_NiUsuarioInexistente_SePuedenDesactivar()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(ana.Id, false, ana.Id), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new CambiarEstadoUsuarioDominio(Guid.NewGuid(), false, Actor), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CasoUso().EjecutarAsync(null!, CancellationToken.None));
        Assert.True(ana.Activo);
    }
}

public class CambiarRolUsuarioCasoUsoTests : CasosUsoIdentidadBase
{
    private static readonly Guid Actor = Guid.NewGuid();

    private CambiarRolUsuarioCasoUso CasoUso() =>
        new(new UsuarioDominio(Usuarios), Usuarios, Auditoria, UnidadTrabajo, new RelojFijo(Ahora));

    [Fact]
    public async Task CambiaElRolYLoAudita()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300", RolesSistema.OperadorLogisticoId);

        var usuario = await CasoUso().EjecutarAsync(new CambiarRolUsuarioDominio(ana.Id, "administrador", Actor), CancellationToken.None);

        Assert.Equal(RolesSistema.AdministradorId, usuario.RolId);
        var evento = Assert.Single(Auditoria.Eventos);
        Assert.Equal("OperadorLogistico", Valores(evento.ValoresAntes)["rol"]);
        Assert.Equal("Administrador", Valores(evento.ValoresDespues)["rol"]);
    }

    [Fact]
    public async Task MismoRol_NoAudita()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300");

        await CasoUso().EjecutarAsync(new CambiarRolUsuarioDominio(ana.Id, "Administrador", Actor), CancellationToken.None);

        Assert.Empty(Auditoria.Eventos);
        Assert.Equal(0, Usuarios.Modificados);
    }

    [Fact]
    public async Task ConductorRolInvalidoOUnoMismo_SeRechazan()
    {
        var conductor = Usuarios.Registrar("Carlos", null, "+57302", RolesSistema.ConductorId);
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new CambiarRolUsuarioDominio(conductor.Id, "Administrador", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new CambiarRolUsuarioDominio(ana.Id, "Pasajero", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new CambiarRolUsuarioDominio(ana.Id, "OperadorLogistico", ana.Id), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => CasoUso().EjecutarAsync(null!, CancellationToken.None));
        Assert.Empty(Auditoria.Eventos);
    }
}

public class ConsultasIdentidadCasosUsoTests : CasosUsoIdentidadBase
{
    [Fact]
    public async Task Resolver_SoloDevuelveUsuariosActivos()
    {
        var ana = Usuarios.Registrar("Ana", "ana@a.co", "+57300");
        Usuarios.Registrar("Beto", "beto@a.co", "+57301", activo: false);
        var casoUso = new ResolverIdentidadCasoUso(Usuarios);

        Assert.Same(ana, await casoUso.EjecutarAsync(new ResolverIdentidadDominio("sub-ana@a.co"), CancellationToken.None));
        Assert.Null(await casoUso.EjecutarAsync(new ResolverIdentidadDominio("sub-beto@a.co"), CancellationToken.None));
        Assert.Null(await casoUso.EjecutarAsync(new ResolverIdentidadDominio("sub-otro"), CancellationToken.None));
        Assert.Null(await casoUso.EjecutarAsync(new ResolverIdentidadDominio(" "), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => casoUso.EjecutarAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Consultar_FiltraPorRol()
    {
        Usuarios.Registrar("Ana", "ana@a.co", "+57300");
        Usuarios.Registrar("Carlos", null, "+57302", RolesSistema.ConductorId);
        var casoUso = new ConsultarUsuariosCasoUso(Usuarios);

        Assert.Equal(2, (await casoUso.EjecutarAsync(new ConsultarUsuariosDominio(null), CancellationToken.None)).Count);
        Assert.Equal("Carlos", Assert.Single(await casoUso.EjecutarAsync(new ConsultarUsuariosDominio("conductor"), CancellationToken.None)).Nombre);
        Assert.Empty(await casoUso.EjecutarAsync(new ConsultarUsuariosDominio("Root"), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => casoUso.EjecutarAsync(null!, CancellationToken.None));
    }
}
