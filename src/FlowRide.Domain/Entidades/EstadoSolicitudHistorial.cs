using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

/// <summary>Registro inmutable de un cambio de estado de una solicitud (PAS-CONF-02, OPLOG-AUD-01).</summary>
public sealed class EstadoSolicitudHistorial
{
    public EstadoSolicitudHistorial(
        Guid id,
        Guid solicitudId,
        EstadoSolicitud? estadoAnterior,
        EstadoSolicitud estadoNuevo,
        DateTimeOffset fechaHora,
        Guid? actorId,
        string? motivo)
    {
        Id = Guardas.Id(id, "El registro de historial requiere un Id.", nameof(id));
        SolicitudId = Guardas.Id(solicitudId, "El registro de historial requiere una solicitud.", nameof(solicitudId));
        EstadoAnterior = estadoAnterior;
        EstadoNuevo = estadoNuevo;
        FechaHora = fechaHora;
        ActorId = actorId;
        Motivo = Guardas.TextoOpcional(motivo);
    }

    public Guid Id { get; }
    public Guid SolicitudId { get; }
    public EstadoSolicitud? EstadoAnterior { get; }
    public EstadoSolicitud EstadoNuevo { get; }
    public DateTimeOffset FechaHora { get; }

    /// <summary>Usuario que provocó el cambio; null si lo hizo el sistema (por ejemplo, el motor).</summary>
    public Guid? ActorId { get; }

    public string? Motivo { get; }
}
