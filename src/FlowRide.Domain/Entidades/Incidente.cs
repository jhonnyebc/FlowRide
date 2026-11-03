using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

/// <summary>Novedad reportada durante un viaje (retraso, falla mecánica, abandono de turno…) (RF-18).</summary>
public sealed class Incidente
{
    public Incidente(Guid id, Guid viajeId, TipoIncidente tipo, string descripcion, DateTimeOffset fechaHora, Guid reportadoPorId)
    {
        Id = Guardas.Id(id, "El incidente requiere un Id.", nameof(id));
        ViajeId = Guardas.Id(viajeId, "El incidente requiere un viaje.", nameof(viajeId));
        Tipo = tipo;
        Descripcion = Guardas.Texto(descripcion, nameof(descripcion));
        FechaHora = fechaHora;
        ReportadoPorId = Guardas.Id(reportadoPorId, "El incidente requiere quién lo reporta.", nameof(reportadoPorId));
    }

    public Guid Id { get; }
    public Guid ViajeId { get; }
    public TipoIncidente Tipo { get; }
    public string Descripcion { get; }
    public DateTimeOffset FechaHora { get; }
    public Guid ReportadoPorId { get; }
}
