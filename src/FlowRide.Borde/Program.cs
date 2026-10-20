using FlowRide.Borde.Rutas;
using FlowRide.Borde.Seguridad;
using FlowRide.Borde.Webhooks;
using FlowRide.Infrastructure;
using Yarp.ReverseProxy.Configuration;

namespace FlowRide.Borde;

/// <summary>
/// Borde común de la plataforma (ADR-0012). Secretos requeridos (user-secrets flowride-borde o variables de entorno,
/// ADR-0014): <c>ConnectionStrings:Catalogo</c>, <c>Borde:WhatsApp:AppSecret</c> y <c>Borde:WhatsApp:VerifyToken</c>.
/// Clase con nombre propio (no el <c>Program</c> de nivel superior) para no chocar con el de la API en las pruebas.
/// </summary>
internal static class ProgramaBorde
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var opciones = builder.Configuration.GetSection(BordeOptions.Seccion).Get<BordeOptions>() ?? new BordeOptions();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = opciones.TamanoMaximoMb * 1024L * 1024L;
        });

        builder.Services.AddSingleton(opciones);
        builder.Services.AddBordeComun(builder.Configuration);
        builder.Services.AddSingleton<ProveedorRutasBorde>();
        builder.Services.AddSingleton<IProxyConfigProvider>(proveedor => proveedor.GetRequiredService<ProveedorRutasBorde>());
        builder.Services.AddSingleton<RecargaRutasBorde>();
        builder.Services.AddHostedService(proveedor => proveedor.GetRequiredService<RecargaRutasBorde>());
        builder.Services.AddReverseProxy();
        builder.Services.AddHttpClient(WebhookEndpoints.ClienteStacks, cliente => cliente.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddLimitesTasaBorde(opciones);
        builder.Services.AddHealthChecks();

        var app = builder.Build();

        app.UseCabecerasSeguridad(opciones);
        app.UseMetodosPermitidos();
        if (!app.Environment.IsEnvironment("Testing"))
            app.UseHttpsRedirection();
        app.UseRateLimiter();

        app.MapHealthChecks("/salud");
        app.MapWebhooksWhatsApp(opciones);
        app.MapReverseProxy();

        app.Run();
    }
}
