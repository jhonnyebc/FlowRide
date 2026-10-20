using FlowRide.Application.Borde.Salida.Puertos;
using FlowRide.Infrastructure.Canal;

namespace FlowRide.Infrastructure.Borde;

/// <summary>
/// Secretos de la app de Meta de la plataforma que usa el borde (ADR-0012, ADR-0014): viven en el gestor de secretos de
/// la plataforma (user-secrets <c>flowride-borde</c> en local), nunca en appsettings.
/// </summary>
public sealed class BordeWhatsAppOptions
{
    public const string Seccion = "Borde:WhatsApp";

    public string? AppSecret { get; set; }
    public string? VerifyToken { get; set; }
}

/// <summary>El webhook de Meta con el mismo formato y la misma firma HMAC que valida el stack (<see cref="FormatoWebhookMeta"/>).</summary>
public sealed class VerificadorWebhookMeta(BordeWhatsAppOptions opciones) : IVerificadorWebhook
{
    private readonly WhatsAppOptions _formato = new() { AppSecret = opciones.AppSecret, VerifyToken = opciones.VerifyToken };

    public bool VerificarSuscripcion(string? modo, string? token) => FormatoWebhookMeta.VerificarSuscripcion(_formato, modo, token);

    public bool FirmaValida(string cuerpo, string? firma) => FormatoWebhookMeta.FirmaValida(_formato, cuerpo, firma);

    public IReadOnlyList<string> NumerosDestino(string cuerpo) => FormatoWebhookMeta.NumerosDestino(cuerpo);
}
