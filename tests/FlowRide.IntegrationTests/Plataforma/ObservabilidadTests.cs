using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.Extensions.Logging;

namespace FlowRide.IntegrationTests.Plataforma;

/// <summary>
/// Cada solicitud deja un log estructurado con traceId, tenantId, componente y resultado
/// (restricción técnica 15, ADR-0011, RF-50). El timestamp lo agrega el formateador JSON.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class ObservabilidadTests(PostgresFixture postgres)
{
    private const int EventoSolicitudCompletada = 1000;

    [Fact]
    public async Task CadaSolicitud_RegistraTraceIdTenantComponenteYResultado()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion, tenantId: "operador-norte");

        await api.CreateClient().GetAsync("/no-existe");

        var entrada = Assert.Single(api.Registro.Entradas, e => e.EventId.Id == EventoSolicitudCompletada);
        Assert.Equal(LogLevel.Information, entrada.Nivel);
        Assert.Equal("operador-norte", entrada.Valores["TenantId"]);
        Assert.Equal("api", entrada.Valores["Componente"]);
        Assert.Matches("^[0-9a-f]{32}$", entrada.Valores["TraceId"]?.ToString());
        Assert.Equal("error_cliente", entrada.Valores["Resultado"]);
        Assert.Equal(404, entrada.Valores["CodigoEstado"]);
    }

    [Fact]
    public async Task RutasDeSalud_NoGeneranRuidoEnElLogOperativo()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion);

        await api.CreateClient().GetAsync("/health/live");
        await api.CreateClient().GetAsync("/health/ready");

        // Se emiten en Debug, por debajo del nivel mínimo por defecto (Information).
        Assert.DoesNotContain(api.Registro.Entradas,
            e => e.EventId.Id == EventoSolicitudCompletada && e.Nivel >= LogLevel.Information);
    }
}
