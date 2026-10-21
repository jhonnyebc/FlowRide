using System.Text.Json.Serialization;
using FlowRide.Domain.Canal.GestionCanal;

namespace FlowRide.Application.Canal.Salida.Puertos;

/// <summary>
/// Contrato de FlowRide con el proveedor de WhatsApp (ADR-0008). Las implementaciones viven en Infrastructure: el número
/// de prueba de Meta Cloud API (<c>WhatsAppSandboxAdapter</c>) y una simulada en memoria. Application nunca ve payloads,
/// URLs, tokens ni códigos de error del proveedor.
/// </summary>
public interface IWhatsAppGateway
{
    /// <summary>Verificación de la suscripción del webhook (el proveedor envía el token configurado).</summary>
    bool VerificarSuscripcion(string? modo, string? token);

    /// <summary>¿La firma del webhook corresponde al cuerpo? (<c>X-Hub-Signature-256</c> en Meta)</summary>
    bool FirmaValida(string cuerpo, string? firma);

    /// <summary>Normaliza el webhook. Lanza <see cref="FormatException"/> si el cuerpo no tiene el formato esperado.</summary>
    WebhookTraducido Traducir(string cuerpo);

    Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct);

    /// <summary>Plantilla aprobada (fuera de la ventana de 24 h, ADR-0016), con los valores de sus variables en orden.</summary>
    Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros,
        CancellationToken ct);
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoResultadoEnvio
{
    Enviado,

    /// <summary>Timeout, límite de tasa o error temporal del proveedor: se reintenta.</summary>
    ErrorTransitorio,

    /// <summary>Destinatario inválido, credenciales rechazadas, fuera de la ventana de conversación…: no se reintenta.</summary>
    ErrorPermanente
}

public sealed record ResultadoEnvio(TipoResultadoEnvio Tipo, string? IdExterno, string? Error)
{
    public static ResultadoEnvio Enviado(string idExterno) => new(TipoResultadoEnvio.Enviado, idExterno, null);
    public static ResultadoEnvio Transitorio(string error) => new(TipoResultadoEnvio.ErrorTransitorio, null, error);
    public static ResultadoEnvio Permanente(string error) => new(TipoResultadoEnvio.ErrorPermanente, null, error);
}

/// <summary>Lado de lectura del canal: la conversación de un número con el bot, para el panel.</summary>
public interface IConsultasCanal
{
    Task<IReadOnlyList<MensajeCanalLectura>> ConversacionAsync(string telefono, int maximo, CancellationToken ct);
}

/// <summary>Un mensaje de la conversación. Direccion: <c>entrante</c> o <c>saliente</c>.</summary>
public sealed record MensajeCanalLectura(
    Guid Id,
    string Direccion,
    string Contenido,
    DateTimeOffset FechaHora,
    string Estado,
    string? EstadoExterno,
    string? UltimoError);
