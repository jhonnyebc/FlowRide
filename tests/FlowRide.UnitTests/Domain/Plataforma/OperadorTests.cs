using FlowRide.Domain.Plataforma;

namespace FlowRide.UnitTests.Domain.Plataforma;

public class OperadorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private static Operador Crear(string id = "tenant-a") => new(id, "Transportes A", "900123456-1", "ops@a.co", Ahora);

    [Fact]
    public void NuevoOperador_QuedaAprovisionandoConSusDatos()
    {
        var operador = Crear();

        Assert.Equal("tenant-a", operador.Id);
        Assert.Equal(EstadoOperador.Aprovisionando, operador.Estado);
        Assert.Equal("Transportes A", operador.RazonSocial);
        Assert.Equal(Ahora, operador.CreadoEn);
    }

    [Fact]
    public void NombresDerivados_SiguenLaConvencionDelSilo()
    {
        var operador = Crear("operador-norte-2");

        Assert.Equal("flowride_operador_norte_2", operador.NombreSilo);
        Assert.Equal("flowride-operador-norte-2", operador.AmbitoSecretos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Tenant-A")]
    [InlineData("tenant_a")]
    [InlineData("-tenant")]
    [InlineData("tenant-")]
    [InlineData("tenant--a")]
    [InlineData("tenant a")]
    public void IdInvalido_SeRechaza(string id) => Assert.Throws<ArgumentException>(() => Crear(id));

    [Fact]
    public void IdDeMasDe40Caracteres_SeRechaza() => Assert.False(Operador.EsIdValido(new string('a', 41)));

    [Fact]
    public void IdNulo_NoEsValido() => Assert.False(Operador.EsIdValido(null));

    [Theory]
    [InlineData("", "900", "a@a.co")]
    [InlineData("A", " ", "a@a.co")]
    [InlineData("A", "900", "")]
    public void DatosVacios_SeRechazan(string razonSocial, string nit, string correo) =>
        Assert.ThrowsAny<ArgumentException>(() => new Operador("tenant-a", razonSocial, nit, correo, Ahora));

    [Fact]
    public void ActualizarDatos_RecortaEspacios()
    {
        var operador = Crear();

        operador.ActualizarDatos("  Transportes Nuevo ", " 900 ", " nuevo@a.co ");

        Assert.Equal("Transportes Nuevo", operador.RazonSocial);
        Assert.Equal("900", operador.Nit);
        Assert.Equal("nuevo@a.co", operador.CorreoContacto);
    }

    [Fact]
    public void Activar_PasaAActivo()
    {
        var operador = Crear();

        operador.Activar();

        Assert.Equal(EstadoOperador.Activo, operador.Estado);
    }

    [Fact]
    public void Suspendido_NoSeActivaAlReaprovisionar()
    {
        var operador = Crear();
        operador.Suspender();

        Assert.Throws<InvalidOperationException>(operador.Activar);
    }

    [Fact]
    public void Reactivar_SoloDesdeSuspendido()
    {
        var operador = Crear();
        Assert.Throws<InvalidOperationException>(operador.Reactivar);

        operador.Suspender();
        operador.Reactivar();

        Assert.Equal(EstadoOperador.Activo, operador.Estado);
    }
}
