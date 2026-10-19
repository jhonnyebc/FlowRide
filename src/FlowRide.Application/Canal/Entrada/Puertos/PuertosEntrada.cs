using FlowRide.Application.Canal.Entrada.Dto;
using FlowRide.Application.Canal.Salida.Puertos;

namespace FlowRide.Application.Canal.Entrada.Puertos;

/// <summary>Puerto de entrada del canal WhatsApp: webhook, worker del canal y consultas del panel.</summary>
public interface ICanalWhatsAppPuertoEntrada
{
    /// <summary>Verificación de la suscripción del webhook: true si el token coincide.</summary>
    bool VerificarSuscripcion(string? modo, string? token);

    Task<WebhookRespuesta> RecibirWebhookAsync(string cuerpo, string? firma, CancellationToken ct);

    Task SimularMensajeAsync(MensajeSimuladoPeticion peticion, CancellationToken ct);

    Task<IReadOnlyList<Guid>> EntrantesPendientesAsync(int maximo, CancellationToken ct);

    /// <summary>Procesa un mensaje entrante; false si ya no estaba pendiente.</summary>
    Task<bool> ProcesarEntranteAsync(Guid mensajeId, CancellationToken ct);

    /// <summary>Registra un error técnico; true si el mensaje agotó sus intentos.</summary>
    Task<bool> RegistrarFalloEntranteAsync(Guid mensajeId, string error, CancellationToken ct);

    Task<IReadOnlyList<Guid>> SalientesPendientesAsync(int maximo, CancellationToken ct);

    /// <summary>Envía un mensaje: Enviado, ErrorTransitorio, ErrorPermanente, o null si no estaba listo.</summary>
    Task<string?> EnviarSalienteAsync(Guid mensajeId, CancellationToken ct);

    Task<IReadOnlyList<MensajeCanalLectura>> ConversacionAsync(string telefono, CancellationToken ct);
}
