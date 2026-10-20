using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Domain.Plataforma;

public class RutaOperadorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ruta_NormalizaSubdominioYUrl()
    {
        var ruta = new RutaOperador("tenant-a", " Tenant-A ", "http://localhost:5237/", " 123456789 ", Ahora);

        Assert.Equal(("tenant-a", "http://localhost:5237", "123456789"), (ruta.Subdominio, ruta.UrlStack, ruta.PhoneNumberId));
        Assert.Equal("tenant-a.dev.localhost", ruta.Host(".Dev.Localhost"));
        Assert.Null(new RutaOperador("tenant-a", "a", "https://api.interna", "  ", Ahora).PhoneNumberId);
    }

    [Theory]
    [InlineData("Tenant A")]
    [InlineData("-tenant")]
    [InlineData("tenant-")]
    [InlineData("")]
    public void Ruta_RechazaSubdominiosInvalidos(string subdominio) =>
        Assert.Throws<FlowRideExcepcion>(() => new RutaOperador("tenant-a", subdominio, "http://localhost:5237", null, Ahora));

    [Theory]
    [InlineData("localhost:5237")]
    [InlineData("ftp://localhost")]
    [InlineData("http://localhost:5237/api")]
    [InlineData("http://localhost:5237?x=1")]
    [InlineData("http://usuario:clave@localhost")]
    public void Ruta_RechazaUrlsQueNoSonLaBaseDelStack(string url) =>
        Assert.Throws<FlowRideExcepcion>(() => new RutaOperador("tenant-a", "tenant-a", url, null, Ahora));

    [Fact]
    public void Ruta_ValidaElOperadorYElNumero()
    {
        Assert.Throws<FlowRideExcepcion>(() => new RutaOperador("Tenant A", "a", "http://localhost", null, Ahora));
        Assert.Throws<FlowRideExcepcion>(() => new RutaOperador("tenant-a", "a", "http://localhost", "+57300", Ahora));

        var ruta = new RutaOperador("tenant-a", "a", "http://localhost", null, Ahora);
        ruta.Actualizar("b", "https://otro:8080", "98765", Ahora.AddDays(1));
        Assert.Equal(("b", "https://otro:8080", "98765", Ahora.AddDays(1)), (ruta.Subdominio, ruta.UrlStack, ruta.PhoneNumberId, ruta.ActualizadaEn));
    }

    [Fact]
    public void Tabla_BuscaPorNumero_YNoAdmiteDuplicados()
    {
        var a = new RutaOperador("tenant-a", "tenant-a", "http://a", "11111", Ahora);
        var b = new RutaOperador("tenant-b", "tenant-b", "http://b", null, Ahora);
        var tabla = new TablaRutas([b, a]);

        Assert.Equal(["tenant-a", "tenant-b"], tabla.Rutas.Select(r => r.OperadorId));
        Assert.Same(a, tabla.PorNumero("11111"));
        Assert.Null(tabla.PorNumero("22222"));
        Assert.Empty(TablaRutas.Vacia.Rutas);

        Assert.Throws<FlowRideExcepcion>(() => new TablaRutas([a, new RutaOperador("tenant-c", "tenant-a", "http://c", null, Ahora)]));
        Assert.Throws<FlowRideExcepcion>(() => new TablaRutas([a, new RutaOperador("tenant-c", "tenant-c", "http://c", "11111", Ahora)]));
        Assert.Throws<ArgumentException>(() => a.Host(" "));
    }
}
