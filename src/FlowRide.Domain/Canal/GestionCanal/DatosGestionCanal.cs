namespace FlowRide.Domain.Canal.GestionCanal;

// Modelo interno del canal WhatsApp (ADR-0008): lo que queda de un webhook del proveedor después de normalizarlo. El
// resto de FlowRide trabaja con esto, nunca con el payload original.

/// <summary>Mensaje entrante normalizado (el <c>IncomingWhatsAppMessage</c> del ADR-0008). Teléfono en E.164.</summary>
public sealed record MensajeRecibido(string IdExterno, string Telefono, string Tipo, string Contenido, DateTimeOffset RecibidoEn)
{
    public const string TipoTexto = "texto";
    public const string TipoNoSoportado = "no_soportado";
}

/// <summary>Estado de entrega de un mensaje que FlowRide envió: sent, delivered, read o failed.</summary>
public sealed record EstadoEntrega(string IdExterno, string Estado, string? Error);

/// <summary>Webhook traducido: mensajes, estados de entrega y cuántos eventos se ignoraron (otro número u otro campo).</summary>
public sealed record WebhookTraducido(IReadOnlyList<MensajeRecibido> Mensajes, IReadOnlyList<EstadoEntrega> Estados, int Ignorados);

// Datos de entrada de los casos de uso del canal.

public sealed record RecibirWebhookDominio(string Cuerpo, string? Firma);

/// <summary>Mensaje inyectado en desarrollo con el proveedor simulado (sin webhook ni firma).</summary>
public sealed record SimularMensajeDominio(string Telefono, string Texto);

public sealed record ProcesarMensajeEntranteDominio(Guid MensajeId);

public sealed record RegistrarFalloEntranteDominio(Guid MensajeId, string Error);

public sealed record EnviarMensajeSalienteDominio(Guid MensajeId);

public sealed record ConsultarEntrantesPendientesDominio(int Maximo);

public sealed record ConsultarSalientesPendientesDominio(int Maximo);

public sealed record ConsultarConversacionCanalDominio(string Telefono);
