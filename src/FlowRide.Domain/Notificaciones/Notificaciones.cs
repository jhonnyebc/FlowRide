using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Notificaciones;

public enum SeveridadAlerta
{
    Info,
    Advertencia,

    /// <summary>Además del panel, se envía por correo a los administradores del operador (decisión del 2026-10-18).</summary>
    Critica
}

/// <summary>
/// Alerta operativa del Panel Web (RF-45, ADR-0015): la ven el Administrador y el Operador logístico, en vivo (SSE) y en
/// la lista. Se guarda para que no se pierda si nadie está conectado.
/// </summary>
public sealed class Alerta
{
#pragma warning disable CS8618 // Solo para EF Core.
    private Alerta() { }
#pragma warning restore CS8618

    public Alerta(Guid id, string tipo, SeveridadAlerta severidad, string titulo, string detalle, DateTimeOffset creadaEn,
        Guid? referenciaId = null, Guid? eventId = null)
    {
        Id = Guardas.Id(id, "La alerta requiere un Id.", nameof(id));
        Tipo = Guardas.Texto(tipo, nameof(tipo));
        Severidad = severidad;
        Titulo = Guardas.Texto(titulo, nameof(titulo));
        Detalle = Guardas.Texto(detalle, nameof(detalle));
        CreadaEn = creadaEn;
        ReferenciaId = referenciaId;
        EventId = eventId;
    }

    public Guid Id { get; private set; }

    /// <summary>Orden de llegada (para el envío en vivo: "las posteriores a la última que vi").</summary>
    public long Secuencia { get; private set; }

    /// <summary>Clave del tipo de alerta (catálogo de mensajes), p. ej. <c>alerta.solicitud.sin_candidatos</c>.</summary>
    public string Tipo { get; private set; }

    public SeveridadAlerta Severidad { get; private set; }
    public string Titulo { get; private set; }
    public string Detalle { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }

    /// <summary>Recurso al que se refiere (solicitud, viaje, conductor…).</summary>
    public Guid? ReferenciaId { get; private set; }

    /// <summary>Evento de la Outbox que la originó.</summary>
    public Guid? EventId { get; private set; }

    public DateTimeOffset? LeidaEn { get; private set; }
    public Guid? LeidaPorId { get; private set; }

    public bool MarcarLeida(Guid usuarioId, DateTimeOffset ahora)
    {
        if (LeidaEn is not null)
            return false;
        LeidaEn = ahora;
        LeidaPorId = usuarioId;
        return true;
    }
}

public enum EstadoCorreo
{
    Pendiente,
    Enviado,
    Fallido
}

/// <summary>Correo pendiente de envío (ADR-0015, <c>IEmailSender</c>): se guarda junto con la alerta y se envía con reintentos.</summary>
public sealed class CorreoSaliente
{
    public const int MaximoIntentos = 5;

#pragma warning disable CS8618 // Solo para EF Core.
    private CorreoSaliente() { }
#pragma warning restore CS8618

    public CorreoSaliente(Guid id, string destinatario, string asunto, string cuerpo, DateTimeOffset creadoEn, Guid? alertaId = null)
    {
        Id = Guardas.Id(id, "El correo requiere un Id.", nameof(id));
        Destinatario = Guardas.Texto(destinatario, nameof(destinatario));
        Asunto = Guardas.Texto(asunto, nameof(asunto));
        Cuerpo = Guardas.Texto(cuerpo, nameof(cuerpo));
        CreadoEn = creadoEn;
        ProximoIntento = creadoEn;
        AlertaId = alertaId;
        Estado = EstadoCorreo.Pendiente;
    }

    public Guid Id { get; private set; }
    public string Destinatario { get; private set; }
    public string Asunto { get; private set; }
    public string Cuerpo { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public Guid? AlertaId { get; private set; }
    public EstadoCorreo Estado { get; private set; }
    public int Intentos { get; private set; }
    public DateTimeOffset ProximoIntento { get; private set; }
    public DateTimeOffset? EnviadoEn { get; private set; }
    public string? UltimoError { get; private set; }

    public bool ListoParaEnviar(DateTimeOffset ahora) => Estado == EstadoCorreo.Pendiente && ProximoIntento <= ahora;

    public void MarcarEnviado(DateTimeOffset ahora)
    {
        Exigir("marcar enviado");
        Intentos++;
        EnviadoEn = ahora;
        Estado = EstadoCorreo.Enviado;
    }

    /// <summary>Error al enviar: se reintenta con la misma espera creciente que WhatsApp; al quinto queda Fallido.</summary>
    public void RegistrarFallo(string error, DateTimeOffset ahora)
    {
        Exigir("reintentar");
        Intentos++;
        UltimoError = Guardas.Texto(error, nameof(error));
        if (Intentos >= MaximoIntentos)
        {
            Estado = EstadoCorreo.Fallido;
            return;
        }
        ProximoIntento = ahora + Canal.MensajeSaliente.Esperas[Math.Min(Intentos - 1, Canal.MensajeSaliente.Esperas.Count - 1)];
    }

    private void Exigir(string accion)
    {
        if (Estado != EstadoCorreo.Pendiente)
            throw Guardas.TransicionInvalida(accion, "el correo", Estado);
    }
}

public enum EstadoPlantilla
{
    Pendiente,
    Aprobada,
    Rechazada
}

/// <summary>
/// Registro de la plantilla de WhatsApp de una clave del catálogo (ADR-0016): fuera de la ventana de 24 h solo se puede
/// escribir con una plantilla aprobada por Meta en la cuenta del operador.
/// </summary>
public sealed class PlantillaWhatsApp
{
#pragma warning disable CS8618 // Solo para EF Core.
    private PlantillaWhatsApp() { }
#pragma warning restore CS8618

    public PlantillaWhatsApp(string clave, string nombre, string idioma, EstadoPlantilla estado = EstadoPlantilla.Pendiente)
    {
        Clave = Guardas.Texto(clave, nameof(clave));
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        Idioma = Guardas.Texto(idioma, nameof(idioma));
        Estado = estado;
        ModificadaEn = DateTimeOffset.UnixEpoch;
    }

    public string Clave { get; private set; }

    /// <summary>Nombre de la plantilla en la cuenta de WhatsApp Business del operador.</summary>
    public string Nombre { get; private set; }

    /// <summary>Código de idioma de Meta, p. ej. <c>es_CO</c>.</summary>
    public string Idioma { get; private set; }

    public EstadoPlantilla Estado { get; private set; }
    public Guid? ModificadaPorId { get; private set; }
    public DateTimeOffset ModificadaEn { get; private set; }

    public bool Utilizable => Estado == EstadoPlantilla.Aprobada;

    public void Actualizar(string nombre, string idioma, EstadoPlantilla estado, Guid? actorId, DateTimeOffset ahora)
    {
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        Idioma = Guardas.Texto(idioma, nameof(idioma));
        Estado = estado;
        ModificadaPorId = actorId;
        ModificadaEn = ahora;
    }
}

/// <summary>Reglas del canal WhatsApp para notificar.</summary>
public static class VentanaConversacion
{
    /// <summary>Ventana de servicio de WhatsApp: se puede escribir texto libre hasta 24 h después del último mensaje del usuario.</summary>
    public static readonly TimeSpan Duracion = TimeSpan.FromHours(24);

    public static bool Abierta(DateTimeOffset? ultimoMensajeDelUsuario, DateTimeOffset ahora) =>
        ultimoMensajeDelUsuario is { } ultimo && ahora - ultimo < Duracion;
}
