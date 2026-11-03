using Microsoft.Extensions.FileProviders;

namespace FlowRide.Borde;

/// <summary>
/// El borde sirve el Panel Web compilado (ADR-0019) en el subdominio de cada operador cuando
/// <see cref="BordeOptions.PanelWeb"/> está definido: sus archivos tal cual y, para cualquier otra navegación, su
/// <c>index.html</c> (la SPA resuelve la ruta en el navegador). <c>/api</c> y <c>/health</c> nunca caen aquí: son del
/// stack del operador, y un host sin ruta sigue respondiendo 404 en ellas.
/// </summary>
public static class PanelWebBorde
{
    public static void UsePanelWeb(this WebApplication app, BordeOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        if (string.IsNullOrWhiteSpace(opciones.PanelWeb))
            return;

        var carpeta = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, opciones.PanelWeb));
        var indice = Path.Combine(carpeta, "index.html");
        if (!File.Exists(indice))
        {
            // El borde sigue funcionando (API, salud y webhooks); solo falta compilar el panel.
            app.Logger.LogWarning(
                "No se sirve el Panel Web: falta {Indice}. Compílalo con 'npm run build' en web/ o quita Borde:PanelWeb.", indice);
            return;
        }

        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(carpeta) });
        // Solo para lo que ninguna otra ruta atendió (las del stack y las del borde van primero) y no parece un archivo.
        app.MapFallback(async contexto =>
        {
            if (!EsNavegacionDelPanel(contexto.Request))
            {
                contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            // index.html siempre se revalida; los recursos de Vite llevan un hash en el nombre.
            contexto.Response.ContentType = "text/html; charset=utf-8";
            contexto.Response.Headers.CacheControl = "no-cache";
            await contexto.Response.SendFileAsync(indice, contexto.RequestAborted);
        });
    }

    private static bool EsNavegacionDelPanel(HttpRequest peticion) =>
        (HttpMethods.IsGet(peticion.Method) || HttpMethods.IsHead(peticion.Method))
        && !peticion.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !peticion.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
}
