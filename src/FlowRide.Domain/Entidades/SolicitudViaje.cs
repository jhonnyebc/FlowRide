namespace FlowRide.Domain.Entidades;

public sealed class SolicitudViaje
{
    public SolicitudViaje(Guid id, DateTimeOffset fechaHoraServicio, int cantidadPasajeros, bool urgente = false)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("La solicitud requiere un Id.", nameof(id));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidadPasajeros);

        Id = id;
        FechaHoraServicio = fechaHoraServicio;
        CantidadPasajeros = cantidadPasajeros;
        Urgente = urgente;
    }

    public Guid Id { get; }
    public DateTimeOffset FechaHoraServicio { get; }
    public int CantidadPasajeros { get; }

    /// <summary>Servicio crítico (RF-13).</summary>
    public bool Urgente { get; }

    public DateOnly FechaServicio => DateOnly.FromDateTime(FechaHoraServicio.DateTime);
}
