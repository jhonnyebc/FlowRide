using System.Net;
using System.Text.Json;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Plataforma;

/// <summary>Errores como ProblemDetails (RFC 9457) con el traceId de los logs (RF-51, ADR-0011).</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class ErroresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task RutaInexistente_DevuelveProblemDetails404ConTraceId()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion);

        var respuesta = await api.CreateClient().GetAsync("/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync());
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.Matches("^[0-9a-f]{32}$", json.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task ExcepcionNoControlada_DevuelveProblemDetails500SinDetallesInternos_YLaRegistra()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion);

        var respuesta = await api.CreateClient().GetAsync(RutaDeErrorDePrueba.Ruta);

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detalle-interno-que-no-debe-filtrarse", cuerpo);

        using var json = JsonDocument.Parse(cuerpo);
        var traceId = json.RootElement.GetProperty("traceId").GetString();
        Assert.Contains(api.Registro.Entradas, entrada =>
            entrada.Nivel == Microsoft.Extensions.Logging.LogLevel.Error
            && Equals(entrada.Valores.GetValueOrDefault("TraceId"), traceId)
            && Equals(entrada.Valores.GetValueOrDefault("TenantId"), "tenant-a"));
    }
}
