namespace FlowRide.Domain.Entidades;

/// <summary>Parada intermedia de un viaje; el orden es único dentro del viaje.</summary>
public sealed class Parada
{
    public Parada(Guid id, Guid viajeId, int orden, string direccion, DateTimeOffset horaEstimada)
    {
        Id = Guardas.Id(id, "La parada requiere un Id.", nameof(id));
        ViajeId = Guardas.Id(viajeId, "La parada requiere un viaje.", nameof(viajeId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orden);
        Orden = orden;
        Direccion = Guardas.Texto(direccion, nameof(direccion));
        HoraEstimada = horaEstimada;
    }

    public Guid Id { get; }
    public Guid ViajeId { get; }
    public int Orden { get; }
    public string Direccion { get; }
    public DateTimeOffset HoraEstimada { get; }
    public DateTimeOffset? HoraReal { get; private set; }

    public void RegistrarLlegada(DateTimeOffset horaReal)
    {
        if (HoraReal is not null)
            throw new InvalidOperationException("La llegada a la parada ya fue registrada.");
        HoraReal = horaReal;
    }
}
