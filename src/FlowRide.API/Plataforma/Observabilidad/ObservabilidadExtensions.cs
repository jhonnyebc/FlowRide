using FlowRide.Infrastructure.Plataforma;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FlowRide.API.Plataforma.Observabilidad;

public static class ObservabilidadExtensions
{
    public const string NombreServicio = "flowride-api";

    /// <summary>
    /// Logs estructurados en JSON y trazas y métricas con OpenTelemetry (ADR-0011).
    /// Cada entrada incluye timestamp UTC, traceId, tenantId, componente y resultado (restricción técnica 15).
    /// El exportador OTLP solo se activa si se define OTEL_EXPORTER_OTLP_ENDPOINT.
    /// </summary>
    public static WebApplicationBuilder AddObservabilidad(this WebApplicationBuilder builder)
    {
        var tenantId = builder.Configuration[$"{SiloOptions.Seccion}:{nameof(SiloOptions.TenantId)}"] ?? "desconocido";

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
        });

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(NombreServicio)
                .AddAttributes([new KeyValuePair<string, object>("flowride.tenant_id", tenantId)]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options => options.Filter = context => !EsRutaDeSalud(context.Request.Path))
                .AddHttpClientInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        return builder;
    }

    internal static bool EsRutaDeSalud(PathString path) => path.StartsWithSegments("/health");
}
