using System.Net;
using System.Text.Json;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Plataforma;

/// <summary>Health checks del stack del operador (RF-53, ADR-0010).</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class SaludTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Live_Responde200SinConsultarLaBase()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion);

        var respuesta = await api.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = await LeerJson(respuesta);
        Assert.Equal("Healthy", json.RootElement.GetProperty("estado").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("verificaciones").GetArrayLength());
    }

    [Fact]
    public async Task Ready_ConLaBaseDelSiloDisponible_Responde200()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion);

        var respuesta = await api.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = await LeerJson(respuesta);
        var verificacion = Assert.Single(json.RootElement.GetProperty("verificaciones").EnumerateArray());
        Assert.Equal("postgres-silo", verificacion.GetProperty("nombre").GetString());
        Assert.Equal("Healthy", verificacion.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Ready_SinLaBaseDelSilo_Responde503SinExponerLaConexion()
    {
        // Valor falso a propósito: la prueba verifica que no aparezca en la respuesta.
        const string baseInalcanzable = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=secreto-que-no-debe-salir;Timeout=2"; // gitleaks:allow
        await using var api = new FlowRideApiFactory(baseInalcanzable);

        var respuesta = await api.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("\"estado\":\"Unhealthy\"", cuerpo);
        Assert.DoesNotContain("secreto-que-no-debe-salir", cuerpo);
        Assert.DoesNotContain("127.0.0.1", cuerpo);
    }

    private static async Task<JsonDocument> LeerJson(HttpResponseMessage respuesta) =>
        await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
}
