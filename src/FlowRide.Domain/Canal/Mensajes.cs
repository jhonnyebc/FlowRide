using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Canal;

public enum EstadoMensajeEntrante
{
    Pendiente,
    Procesado,
    Fallido
}

public enum EstadoMensajeSaliente
{
    Pendiente,
    Enviado,
    Fallido
}

/// <summary>
/// Mensaje recibido por el canal WhatsApp, ya normalizado (ADR-0008). El webhook lo guarda y responde de inmediato; el
/// worker del canal lo procesa después. El identificador externo es único: un reenvío del proveedor no se procesa dos veces.
/// </summary>
public sealed class MensajeEntrante
{
    public const int MaximoIntentos = 5;

#pragma warning disable CS8618 // Solo para EF Core.
    private MensajeEntrante() { }
#pragma warning restore CS8618

    public MensajeEntrante(Guid id, string idExterno, string remitente, string tipo, string contenido, DateTimeOffset recibidoEn)
    {
        Id = Guardas.Id(id, "El mensaje requiere un Id.", nameof(id));
        IdExterno = Guardas.Texto(idExterno, nameof(idExterno));
        Remitente = Guardas.Texto(remitente, nameof(remitente));
        Tipo = Guardas.Texto(tipo, nameof(tipo));
        Contenido = contenido?.Trim() ?? string.Empty;
        RecibidoEn = recibidoEn;
        Estado = EstadoMensajeEntrante.Pendiente;
    }

    public Guid Id { get; private set; }
    public string IdExterno { get; private set; }

    /// <summary>Teléfono del remitente en formato E.164 (+57…).</summary>
    public string Remitente { get; private set; }

    /// <summary>Tipo normalizado: <c>texto</c> o <c>no_soportado</c>.</summary>
    public string Tipo { get; private set; }

    public string Contenido { get; private set; }
    public DateTimeOffset RecibidoEn { get; private set; }
    public EstadoMensajeEntrante Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset? ProcesadoEn { get; private set; }

    /// <summary>Usuario de FlowRide detrás del número, si está registrado.</summary>
    public Guid? UsuarioId { get; private set; }

    public string? UltimoError { get; private set; }

    public void MarcarProcesado(Guid? usuarioId, DateTimeOffset ahora)
    {
        if (Estado != EstadoMensajeEntrante.Pendiente)
            throw Guardas.TransicionInvalida("procesar", "el mensaje", Estado);
        UsuarioId = usuarioId;
        ProcesadoEn = ahora;
        Estado = EstadoMensajeEntrante.Procesado;
    }

    /// <summary>Un error técnico al procesarlo: se reintenta hasta <see cref="MaximoIntentos"/> veces.</summary>
    public void RegistrarFallo(string error)
    {
        if (Estado != EstadoMensajeEntrante.Pendiente)
            return;
        Intentos++;
        UltimoError = Guardas.Texto(error, nameof(error));
        if (Intentos >= MaximoIntentos)
            Estado = EstadoMensajeEntrante.Fallido;
    }
}

/// <summary>
/// Mensaje que FlowRide envía por WhatsApp. Se guarda antes de enviarlo y lo envía el worker del canal con reintentos;
/// guarda el identificador que devuelve el proveedor y el último estado de entrega que reporta (correlación, ADR-0008).
/// </summary>
public sealed class MensajeSaliente
{
    public const int MaximoIntentos = 5;

    /// <summary>Espera antes de cada reintento por un error transitorio.</summary>
    public static readonly IReadOnlyList<TimeSpan> Esperas =
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

#pragma warning disable CS8618 // Solo para EF Core.
    private MensajeSaliente() { }
#pragma warning restore CS8618

    public MensajeSaliente(Guid id, string destinatario, string contenido, DateTimeOffset creadoEn, Guid? enRespuestaA = null)
    {
        Id = Guardas.Id(id, "El mensaje requiere un Id.", nameof(id));
        Destinatario = Guardas.Texto(destinatario, nameof(destinatario));
        Contenido = Guardas.Texto(contenido, nameof(contenido));
        CreadoEn = creadoEn;
        ProximoIntento = creadoEn;
        EnRespuestaA = enRespuestaA;
        Estado = EstadoMensajeSaliente.Pendiente;
    }

    public Guid Id { get; private set; }
    public string Destinatario { get; private set; }
    public string Contenido { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public Guid? EnRespuestaA { get; private set; }
    public EstadoMensajeSaliente Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset ProximoIntento { get; private set; }
    public DateTimeOffset? EnviadoEn { get; private set; }

    /// <summary>Identificador del mensaje en el proveedor (wamid en Meta).</summary>
    public string? IdExterno { get; private set; }

    /// <summary>Último estado de entrega reportado por el proveedor: sent, delivered, read o failed.</summary>
    public string? EstadoExterno { get; private set; }

    public string? UltimoError { get; private set; }

    /// <summary>Clave del catálogo de mensajes con que se armó (ADR-0016); null en las respuestas del bot.</summary>
    public string? ClaveMensaje { get; private set; }

    /// <summary>Evento de la Outbox que originó la notificación (deduplicación, ADR-0015).</summary>
    public Guid? EventId { get; private set; }

    /// <summary>Plantilla aprobada con que se envía fuera de la ventana de 24 h; null si va como texto.</summary>
    public string? Plantilla { get; private set; }

    public string? IdiomaPlantilla { get; private set; }

    /// <summary>Valores de las variables de la plantilla, en orden, como JSON.</summary>
    public string? ParametrosPlantilla { get; private set; }

    public bool EsPlantilla => Plantilla is not null;

    /// <summary>Marca el mensaje como notificación del catálogo (clave y evento de origen).</summary>
    public MensajeSaliente ComoNotificacion(string claveMensaje, Guid eventId)
    {
        ClaveMensaje = Guardas.Texto(claveMensaje, nameof(claveMensaje));
        EventId = eventId;
        return this;
    }

    /// <summary>Se enviará con la plantilla aprobada; <see cref="Contenido"/> queda como el texto equivalente (trazabilidad).</summary>
    public MensajeSaliente ConPlantilla(string nombre, string idioma, string parametrosJson)
    {
        Plantilla = Guardas.Texto(nombre, nameof(nombre));
        IdiomaPlantilla = Guardas.Texto(idioma, nameof(idioma));
        ParametrosPlantilla = Guardas.Texto(parametrosJson, nameof(parametrosJson));
        return this;
    }

    public bool ListoParaEnviar(DateTimeOffset ahora) => Estado == EstadoMensajeSaliente.Pendiente && ProximoIntento <= ahora;

    public void MarcarEnviado(string idExterno, DateTimeOffset ahora)
    {
        ExigirPendiente("marcar enviado");
        Intentos++;
        IdExterno = Guardas.Texto(idExterno, nameof(idExterno));
        EnviadoEn = ahora;
        Estado = EstadoMensajeSaliente.Enviado;
    }

    /// <summary>Error transitorio (timeout, 5xx, límite de tasa): se reintenta con espera creciente.</summary>
    public void ProgramarReintento(string error, DateTimeOffset ahora)
    {
        ExigirPendiente("reintentar");
        Intentos++;
        UltimoError = Guardas.Texto(error, nameof(error));
        if (Intentos >= MaximoIntentos)
        {
            Estado = EstadoMensajeSaliente.Fallido;
            return;
        }
        ProximoIntento = ahora + Esperas[Math.Min(Intentos - 1, Esperas.Count - 1)];
    }

    /// <summary>Error permanente (destinatario inválido, credenciales rechazadas…): no se reintenta.</summary>
    public void MarcarFallido(string error)
    {
        ExigirPendiente("marcar fallido");
        Intentos++;
        UltimoError = Guardas.Texto(error, nameof(error));
        Estado = EstadoMensajeSaliente.Fallido;
    }

    /// <summary>El proveedor reporta la entrega (o su fallo) de un mensaje ya enviado.</summary>
    public void RegistrarEstadoExterno(string estado, string? error)
    {
        EstadoExterno = Guardas.Texto(estado, nameof(estado));
        if (!string.IsNullOrWhiteSpace(error))
            UltimoError = error.Trim();
    }

    private void ExigirPendiente(string accion)
    {
        if (Estado != EstadoMensajeSaliente.Pendiente)
            throw Guardas.TransicionInvalida(accion, "el mensaje", Estado);
    }
}
