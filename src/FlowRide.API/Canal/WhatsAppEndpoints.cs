using FlowRide.API.Identidad;
using FlowRide.Application.Canal.Entrada.Dto;
using FlowRide.Application.Canal.Entrada.Puertos;

namespace FlowRide.API.Canal;

/// <summary>
/// Adaptador de entrada HTTP del canal WhatsApp (RF-14, ADR-0008). El webhook no lleva token del realm: se autentica con
/// la firma del proveedor (<c>X-Hub-Signature-256</c>) y solo guarda el mensaje, para responder en milisegundos; el bot lo
/// procesa en segundo plano. La conversación de un número es del Panel Web.
/// </summary>
public static class WhatsAppEndpoints
{
    public const string CabeceraFirma = "X-Hub-Signature-256";

    public static IEndpointRouteBuilder MapWhatsApp(this IEndpointRouteBuilder app, bool simulacion)
    {
        var whatsapp = app.MapGroup("/whatsapp").WithTags("Canal WhatsApp");

        // Verificación de la suscripción: Meta envía hub.mode, hub.verify_token y hub.challenge.
        whatsapp.MapGet("/webhook", (HttpRequest peticion, ICanalWhatsAppPuertoEntrada puerto) =>
                puerto.VerificarSuscripcion(peticion.Query["hub.mode"], peticion.Query["hub.verify_token"])
                    ? Results.Text(peticion.Query["hub.challenge"].ToString())
                    : Results.StatusCode(StatusCodes.Status403Forbidden))
            .AllowAnonymous();

        whatsapp.MapPost("/webhook", async (HttpRequest peticion, ICanalWhatsAppPuertoEntrada puerto, CancellationToken ct) =>
        {
            using var lector = new StreamReader(peticion.Body);
            var cuerpo = await lector.ReadToEndAsync(ct);
            var resultado = await puerto.RecibirWebhookAsync(cuerpo, peticion.Headers[CabeceraFirma].FirstOrDefault(), ct);
            if (!resultado.FirmaValida)
                return Results.Unauthorized();
            return resultado.FormatoValido ? Results.Ok(resultado) : Results.BadRequest();
        }).AllowAnonymous();

        whatsapp.MapGet("/conversaciones/{telefono}", async (string telefono, ICanalWhatsAppPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.ConversacionAsync(telefono, ct))).RequireAuthorization(Politicas.PanelWeb);

        // Solo en desarrollo con el proveedor simulado: escribirle al bot sin WhatsApp ni firma.
        if (simulacion)
        {
            whatsapp.MapPost("/simulado/mensajes", async (MensajeSimuladoPeticion peticion, ICanalWhatsAppPuertoEntrada puerto, CancellationToken ct) =>
            {
                await puerto.SimularMensajeAsync(peticion, ct);
                return Results.Accepted();
            }).AllowAnonymous();
        }

        return app;
    }
}
