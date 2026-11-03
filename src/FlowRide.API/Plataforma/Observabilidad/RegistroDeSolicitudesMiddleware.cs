using System.Diagnostics;
using FlowRide.Infrastructure.Plataforma;
using Microsoft.Extensions.Options;

namespace FlowRide.API.Plataforma.Observabilidad;

/// <summary>
/// Abre un scope de log con traceId, tenantId y componente para toda la solicitud y registra su resultado
/// (RF-50, ADR-0011). Las rutas de salud se registran en nivel Debug para no generar ruido.
/// </summary>
public sealed partial class RegistroDeSolicitudesMiddleware(
    RequestDelegate next,
    ILogger<RegistroDeSolicitudesMiddleware> logger,
    IOptions<SiloOptions> silo)
{
    private const string Componente = "api";

    public async Task InvokeAsync(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["TraceId"] = traceId,
            ["TenantId"] = silo.Value.TenantId,
            ["Componente"] = Componente
        });

        var inicio = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var duracionMs = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            var codigo = context.Response.StatusCode;
            var nivel = ObservabilidadExtensions.EsRutaDeSalud(context.Request.Path) ? LogLevel.Debug : NivelPara(codigo);

            SolicitudCompletada(logger, nivel, context.Request.Method, context.Request.Path, codigo,
                ResultadoPara(codigo), duracionMs);
        }
    }

    public static string ResultadoPara(int codigoEstado) => codigoEstado switch
    {
        >= 500 => "error_servidor",
        >= 400 => "error_cliente",
        _ => "exito"
    };

    private static LogLevel NivelPara(int codigoEstado) => codigoEstado >= 500 ? LogLevel.Error : LogLevel.Information;

    [LoggerMessage(EventId = 1000,
        Message = "HTTP {Metodo} {Ruta} respondió {CodigoEstado} ({Resultado}) en {DuracionMs:0.0} ms")]
    private static partial void SolicitudCompletada(ILogger logger, LogLevel level, string metodo, string ruta,
        int codigoEstado, string resultado, double duracionMs);
}
