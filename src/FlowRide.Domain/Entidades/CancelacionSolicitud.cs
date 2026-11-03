namespace FlowRide.Domain.Entidades;

/// <summary>Evidencia de la cancelación de una solicitud: quién, cuándo y por qué (PAS-AUD-02).</summary>
public sealed class CancelacionSolicitud
{
    public CancelacionSolicitud(Guid id, Guid solicitudId, string motivo, DateTimeOffset fechaHora, Guid canceladaPorId)
    {
        Id = Guardas.Id(id, "La cancelación requiere un Id.", nameof(id));
        SolicitudId = Guardas.Id(solicitudId, "La cancelación requiere una solicitud.", nameof(solicitudId));
        Motivo = Guardas.Texto(motivo, nameof(motivo));
        FechaHora = fechaHora;
        CanceladaPorId = Guardas.Id(canceladaPorId, "La cancelación requiere el usuario que la hizo.", nameof(canceladaPorId));
    }

    public Guid Id { get; }
    public Guid SolicitudId { get; }
    public string Motivo { get; }
    public DateTimeOffset FechaHora { get; }
    public Guid CanceladaPorId { get; }
}
