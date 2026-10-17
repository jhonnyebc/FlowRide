using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Domain.Plataforma;

/// <summary>Reglas de negocio sobre operadores que consultan el catálogo.</summary>
public class OperadorDominioTests
{
    private readonly OperadoresEnMemoria _operadores = new();

    private OperadorDominio Dominio() => new(_operadores);

    [Fact]
    public async Task ValidarOperadorExista_DevuelveElOperador()
    {
        var operador = _operadores.Registrar("tenant-a");

        Assert.Same(operador, await Dominio().ValidarOperadorExistaAsync("tenant-a", CancellationToken.None));
    }

    [Fact]
    public async Task ValidarOperadorExista_SiNoExiste_Falla()
    {
        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Dominio().ValidarOperadorExistaAsync("tenant-x", CancellationToken.None));

        Assert.Contains("tenant-x", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("tenant-a")]
    [InlineData("operador-2")]
    public void ValidarIdentificador_Valido_NoFalla(string id) => OperadorDominio.ValidarIdentificador(id);

    [Theory]
    [InlineData("Tenant-A")]
    [InlineData("tenant_a")]
    [InlineData("")]
    public void ValidarIdentificador_Invalido_Falla(string id) =>
        Assert.Throws<FlowRideExcepcion>(() => OperadorDominio.ValidarIdentificador(id));

    [Fact]
    public async Task ValidarNitDisponible_NitLibreODelMismoOperador_NoFalla()
    {
        _operadores.Registrar("tenant-a", nit: "900");

        await Dominio().ValidarNitDisponibleAsync("901", "tenant-b", CancellationToken.None);
        await Dominio().ValidarNitDisponibleAsync(" 900 ", "tenant-a", CancellationToken.None);
    }

    [Fact]
    public async Task ValidarNitDisponible_NitDeOtroOperador_Falla()
    {
        _operadores.Registrar("tenant-a", nit: "900");

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Dominio().ValidarNitDisponibleAsync("900", "tenant-b", CancellationToken.None));

        Assert.Contains("tenant-a", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidarNoSuspendido_SoloFallaConSuspendidos()
    {
        OperadorDominio.ValidarNoSuspendido(_operadores.Registrar("tenant-a"));
        Assert.Throws<FlowRideExcepcion>(() =>
            OperadorDominio.ValidarNoSuspendido(_operadores.Registrar("tenant-s", EstadoOperador.Suspendido)));
        Assert.Throws<ArgumentNullException>(() => OperadorDominio.ValidarNoSuspendido(null!));
    }

    [Fact]
    public void MigrarSilosDominio_DistingueUnoDeTodos()
    {
        Assert.True(MigrarSilosDominio.Todos.EsTodos);
        Assert.False(new MigrarSilosDominio("tenant-a").EsTodos);
    }

    [Fact]
    public void FlowRideExcepcion_ConservaMensajeYCausa()
    {
        var causa = new InvalidOperationException("causa");

        var excepcion = new FlowRideExcepcion("regla", causa);

        Assert.Equal("regla", excepcion.Message);
        Assert.Same(causa, excepcion.InnerException);
        Assert.NotNull(new FlowRideExcepcion().Message);
    }
}
