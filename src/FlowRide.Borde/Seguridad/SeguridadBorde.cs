using System.Threading.RateLimiting;
using FlowRide.Borde.Rutas;
using FlowRide.Borde.Webhooks;

namespace FlowRide.Borde.Seguridad;

/// <summary>
/// Defensa del borde (ADR-0012): métodos permitidos, cabeceras de seguridad en toda respuesta (también las que vienen
/// de un stack) y límites de tasa por IP. El WAF con OWASP CRS es el gestionado de la plataforma (Fase 12).
/// </summary>
public static class SeguridadBorde
{
    private static readonly HashSet<string> MetodosPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Get, HttpMethods.Head, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete, HttpMethods.Options
    };

    // Cabeceras de los stacks que delatan la tecnología; el borde no las deja salir.
    private static readonly string[] CabecerasOcultas = ["Server", "X-Powered-By", "X-AspNet-Version"];

    public static IServiceCollection AddLimitesTasaBorde(this IServiceCollection services, BordeOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        return services.AddRateLimiter(limitador =>
        {
            limitador.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limitador.OnRejected = (contexto, _) =>
            {
                contexto.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };
            limitador.AddPolicy(ProveedorRutasBorde.PoliticaLimite, contexto => PorIp(contexto, opciones.PeticionesPorMinutoPanel));
            limitador.AddPolicy(WebhookEndpoints.PoliticaLimite, contexto => PorIp(contexto, opciones.PeticionesPorMinutoWebhook));
        });
    }

    /// <summary>Rechaza con 405 los métodos que ni el Panel Web ni la API usan (TRACE, CONNECT…).</summary>
    public static IApplicationBuilder UseMetodosPermitidos(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            if (!MetodosPermitidos.Contains(contexto.Request.Method))
            {
                contexto.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                return;
            }
            await siguiente(contexto);
        });

    public static IApplicationBuilder UseCabecerasSeguridad(this IApplicationBuilder app, BordeOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        return app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var cabeceras = contexto.Response.Headers;
                foreach (var oculta in CabecerasOcultas) cabeceras.Remove(oculta);
                cabeceras.XContentTypeOptions = "nosniff";
                cabeceras.XFrameOptions = "DENY";
                cabeceras["Referrer-Policy"] = "no-referrer";
                cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                cabeceras["Cross-Origin-Opener-Policy"] = "same-origin";
                cabeceras.ContentSecurityPolicy = opciones.PoliticaContenido;
                if (contexto.Request.IsHttps)
                    cabeceras.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
                return Task.CompletedTask;
            });
            await siguiente(contexto);
        });
    }

    private static RateLimitPartition<string> PorIp(HttpContext contexto, int porMinuto) =>
        RateLimitPartition.GetFixedWindowLimiter(contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-ip", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, porMinuto),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
}
