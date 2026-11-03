using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FlowRide.API.Plataforma.Salud;

/// <summary>Respuesta JSON de los health checks. No expone excepciones ni datos de conexión (RF-53).</summary>
public static class RespuestaDeSalud
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static Task EscribirAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var cuerpo = new
        {
            estado = report.Status.ToString(),
            duracionMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            verificaciones = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new
                {
                    nombre = entry.Key,
                    estado = entry.Value.Status.ToString(),
                    descripcion = entry.Value.Description,
                    duracionMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1)
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, Opciones), context.RequestAborted);
    }
}
