using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FlowRide.Borde;

/// <summary>
/// Logs, trazas y métricas del borde con OpenTelemetry (ADR-0011, RF-50, RF-51, RF-53), igual que la API: incluye las
/// métricas propias del borde (webhooks rechazados, números sin operador, tokens) y las trazas del proxy hacia cada
/// stack. El exportador OTLP solo se activa si se define OTEL_EXPORTER_OTLP_ENDPOINT (en local, el panel de
/// observabilidad del docker-compose).
/// </summary>
public static class ObservabilidadBorde
{
    public const string NombreServicio = "flowride-borde";

    /// <summary>Medidores propios del borde (<c>FlowRide.Borde</c> y <c>FlowRide.Borde.Tokens</c>).</summary>
    private const string MedidoresBorde = "FlowRide.Borde*";

    /// <summary>Fuente de actividades de YARP: una traza por cada reenvío a un stack.</summary>
    private const string FuenteProxy = "Yarp.ReverseProxy";

    public static WebApplicationBuilder AddObservabilidadBorde(this WebApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(NombreServicio))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !EsRutaDeSalud(context.Request.Path))
                .AddHttpClientInstrumentation()
                .AddSource(FuenteProxy))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter(MedidoresBorde));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }

    internal static bool EsRutaDeSalud(PathString path) => path.StartsWithSegments("/salud");
}
