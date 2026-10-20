namespace FlowRide.Application.Borde.Salida.Puertos;

/// <summary>
/// Webhook de WhatsApp en el borde (ADR-0012), con el <c>app secret</c> y el token de verificación de la app de Meta de
/// la plataforma (ADR-0014). Lo implementa Infrastructure con el mismo formato que usa el stack.
/// </summary>
public interface IVerificadorWebhook
{
    /// <summary>Handshake de Meta: modo <c>subscribe</c> y el token de verificación configurado.</summary>
    bool VerificarSuscripcion(string? modo, string? token);

    /// <summary>Firma <c>X-Hub-Signature-256</c> del cuerpo exacto que llegó.</summary>
    bool FirmaValida(string cuerpo, string? firma);

    /// <summary>Los <c>phone_number_id</c> a los que va el evento; <see cref="FormatException"/> si no es un webhook de WhatsApp.</summary>
    IReadOnlyList<string> NumerosDestino(string cuerpo);
}
