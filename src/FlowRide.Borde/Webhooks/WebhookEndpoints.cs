using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Text;
using FlowRide.Application.Borde.Entrada.Puertos;
using FlowRide.Borde.Rutas;
using Microsoft.AspNetCore.Mvc;

namespace FlowRide.Borde.Webhooks;

/// <summary>
/// Router de webhooks de WhatsApp (ADR-0012): el único callback de la app de Meta para todos los operadores. Responde el
/// handshake, valida <c>X-Hub-Signature-256</c>, elige el stack por <c>phone_number_id</c> y le reenvía el cuerpo
/// intacto con su firma. No guarda nada: si un stack no responde, devuelve 502 para que Meta reintente.
/// </summary>
public static partial class WebhookEndpoints
{
    public const string Ruta = "/api/whatsapp/webhook";
    public const string CabeceraFirma = "X-Hub-Signature-256";
    public const string ClienteStacks = "stacks";
    public const string PoliticaLimite = "webhook";

    private static readonly Meter Medidor = new("FlowRide.Borde");
    private static readonly Counter<long> Rechazados = Medidor.CreateCounter<long>("flowride.borde.webhooks_rechazados",
        description: "Webhooks rechazados por firma inválida (posible ataque, ADR-0012).");
    private static readonly Counter<long> SinOperador = Medidor.CreateCounter<long>("flowride.borde.webhooks_sin_operador",
        description: "Eventos de un phone_number_id sin operador registrado.");

    public static IEndpointRouteBuilder MapWebhooksWhatsApp(this IEndpointRouteBuilder app, BordeOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        app.MapGet(Ruta, (HttpRequest peticion, IBordePuertoEntrada puerto) =>
                puerto.VerificarSuscripcion(peticion.Query["hub.mode"], peticion.Query["hub.verify_token"])
                    ? Results.Text(peticion.Query["hub.challenge"].ToString())
                    : Results.StatusCode(StatusCodes.Status403Forbidden))
            .RequireRateLimiting(PoliticaLimite);

        app.MapPost(Ruta, RecibirAsync)
            .WithMetadata(new RequestSizeLimitAttribute(opciones.TamanoWebhookKb * 1024L))
            .RequireRateLimiting(PoliticaLimite);

        return app;
    }

    private static async Task<IResult> RecibirAsync(HttpRequest peticion, IBordePuertoEntrada puerto, ProveedorRutasBorde rutas,
        IHttpClientFactory clientes, ILoggerFactory registros, CancellationToken ct)
    {
        var registro = registros.CreateLogger(typeof(WebhookEndpoints));
        using var memoria = new MemoryStream();
        await peticion.Body.CopyToAsync(memoria, ct);
        var bytes = memoria.ToArray();
        var firma = peticion.Headers[CabeceraFirma].FirstOrDefault();

        var resultado = await puerto.EnrutarWebhookAsync(Encoding.UTF8.GetString(bytes), firma, rutas.Vigentes, ct);
        if (!resultado.FirmaValida)
        {
            Rechazados.Add(1);
            FirmaInvalida(registro, peticion.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida");
            return Results.Unauthorized();
        }
        if (!resultado.FormatoValido)
            return Results.BadRequest();

        foreach (var numero in resultado.NumerosSinOperador)
        {
            SinOperador.Add(1);
            NumeroSinOperador(registro, numero);
        }

        var cliente = clientes.CreateClient(ClienteStacks);
        foreach (var destino in resultado.Destinos)
        {
            using var reenvio = new HttpRequestMessage(HttpMethod.Post, new Uri(destino.UrlStack, Ruta))
            {
                Content = new ByteArrayContent(bytes)
            };
            reenvio.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            reenvio.Headers.TryAddWithoutValidation(CabeceraFirma, firma);
            try
            {
                using var respuesta = await cliente.SendAsync(reenvio, ct);
                if (!respuesta.IsSuccessStatusCode)
                {
                    StackRechazo(registro, destino.TenantId, (int)respuesta.StatusCode);
                    return Results.StatusCode(StatusCodes.Status502BadGateway);
                }
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                StackNoResponde(registro, destino.TenantId, error);
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }
        }
        return Results.Ok();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook rechazado por firma inválida desde {Ip}.")]
    private static partial void FirmaInvalida(ILogger registro, string ip);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook del phone_number_id {Numero} sin operador registrado: no se reenvía.")]
    private static partial void NumeroSinOperador(ILogger registro, string numero);

    [LoggerMessage(Level = LogLevel.Error, Message = "El stack de {Operador} respondió {Estado} al webhook; Meta lo reintentará.")]
    private static partial void StackRechazo(ILogger registro, string operador, int estado);

    [LoggerMessage(Level = LogLevel.Error, Message = "El stack de {Operador} no respondió al webhook; Meta lo reintentará.")]
    private static partial void StackNoResponde(ILogger registro, string operador, Exception error);
}
