using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Identidad;

namespace FlowRide.UnitTests.Domain.Identidad;

public class RolesSistemaTests
{
    [Fact]
    public void LosCuatroRoles_TienenIdYNombreUnicos()
    {
        Assert.Equal(4, RolesSistema.Todos.Count);
        Assert.Equal(4, RolesSistema.Todos.Select(rol => rol.Id).Distinct().Count());
        Assert.Equal(4, RolesSistema.Todos.Select(rol => rol.Nombre).Distinct().Count());
    }

    [Theory]
    [InlineData("Administrador", true)]
    [InlineData(" operadorlogistico ", true)]
    [InlineData("Conductor", false)]
    [InlineData("Pasajero", false)]
    public void PorNombre_IgnoraMayusculasYEspacios_YSabeSiEsDelPanel(string nombre, bool delPanel)
    {
        var rol = RolesSistema.PorNombre(nombre);

        Assert.NotNull(rol);
        Assert.Equal(delPanel, RolesSistema.EsDelPanel(rol.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Root")]
    public void PorNombre_Desconocido_EsNull(string? nombre) => Assert.Null(RolesSistema.PorNombre(nombre));

    [Fact]
    public void NombrePorId_RolQueNoEsDelSistema_Falla()
    {
        Assert.Equal(RolesSistema.Conductor, RolesSistema.NombrePorId(RolesSistema.ConductorId));
        Assert.Throws<InvalidOperationException>(() => RolesSistema.NombrePorId(Guid.NewGuid()));
    }
}

public class UsuarioDominioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private readonly UsuariosEnMemoria _usuarios = new();

    private UsuarioDominio Dominio() => new(_usuarios);

    [Fact]
    public async Task ValidarUsuarioExista_DevuelveElUsuarioOFalla()
    {
        var ana = _usuarios.Registrar("Ana", "ana@a.co", "+57300");

        Assert.Same(ana, await Dominio().ValidarUsuarioExistaAsync(ana.Id, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Dominio().ValidarUsuarioExistaAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CorreoYTelefonoRepetidos_SeRechazan()
    {
        _usuarios.Registrar("Ana", "ana@a.co", "+57300");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Dominio().ValidarCorreoDisponibleAsync(" ana@a.co ", CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Dominio().ValidarTelefonoDisponibleAsync("+57300", CancellationToken.None));
        await Dominio().ValidarCorreoDisponibleAsync("beto@a.co", CancellationToken.None);
        await Dominio().ValidarTelefonoDisponibleAsync("+57301", CancellationToken.None);
    }

    [Theory]
    [InlineData("ana@a.co", true)]
    [InlineData(" ana@a.co ", true)]
    [InlineData("ana", false)]
    [InlineData("Ana <ana@a.co>", false)]
    public void ValidarFormatoCorreo(string correo, bool valido)
    {
        if (valido) UsuarioDominio.ValidarFormatoCorreo(correo);
        else Assert.Throws<FlowRideExcepcion>(() => UsuarioDominio.ValidarFormatoCorreo(correo));
    }

    [Theory]
    [InlineData("Conductor")]
    [InlineData("Root")]
    [InlineData(null)]
    public void ValidarRolDelPanel_SoloAdministradorUOperadorLogistico(string? rol)
    {
        Assert.Equal(RolesSistema.OperadorLogisticoId, UsuarioDominio.ValidarRolDelPanel("OperadorLogistico").Id);
        var error = Assert.Throws<FlowRideExcepcion>(() => UsuarioDominio.ValidarRolDelPanel(rol));
        Assert.Contains("Administrador o OperadorLogistico", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidarUsuarioDelPanel_RechazaPasajerosYConductores()
    {
        var conductor = new Usuario(Guid.NewGuid(), "Carlos", "+57302", RolesSistema.ConductorId, Ahora);

        Assert.Throws<FlowRideExcepcion>(() => UsuarioDominio.ValidarUsuarioDelPanel(conductor));
        UsuarioDominio.ValidarUsuarioDelPanel(_usuarios.Registrar("Ana", "ana@a.co", "+57300"));
    }

    [Fact]
    public void ValidarNoEsElActor_ImpideActuarSobreUnoMismo()
    {
        var ana = _usuarios.Registrar("Ana", "ana@a.co", "+57300");

        Assert.Throws<FlowRideExcepcion>(() => UsuarioDominio.ValidarNoEsElActor(ana, ana.Id));
        UsuarioDominio.ValidarNoEsElActor(ana, Guid.NewGuid());
        UsuarioDominio.ValidarNoEsElActor(ana, null);
    }
}
