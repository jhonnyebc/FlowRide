using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>
/// Parada intermedia de un viaje; el orden es único dentro del viaje. La hora estimada es futura y en un lugar: hora local
/// y zona, con el instante derivado. La hora real ya pasó: instante UTC.
/// </summary>
public sealed class Parada
{
#pragma warning disable CS8618 // Solo para materializar desde la base: la hora estimada se carga desde sus columnas.
    private Parada() { }
#pragma warning restore CS8618

    public Parada(Guid id, Guid viajeId, int orden, string direccion, FechaHoraLocal horaEstimada)
    {
        Id = Guardas.Id(id, "La parada requiere un Id.", nameof(id));
        ViajeId = Guardas.Id(viajeId, "La parada requiere un viaje.", nameof(viajeId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orden);
        Orden = orden;
        Direccion = Guardas.Texto(direccion, nameof(direccion));
        ArgumentNullException.ThrowIfNull(horaEstimada);
        HoraEstimadaLocal = horaEstimada.Hora;
        ZonaHoraria = horaEstimada.Zona;
        HoraEstimada = horaEstimada.Instante();
    }

    public Guid Id { get; }
    public Guid ViajeId { get; }
    public int Orden { get; }
    public string Direccion { get; }
    public DateTime HoraEstimadaLocal { get; }
    public string ZonaHoraria { get; } = string.Empty;
    public DateTimeOffset HoraEstimada { get; private set; }
    public DateTimeOffset? HoraReal { get; private set; }

    /// <summary>Recalcula el instante estimado con las reglas vigentes de la zona; true si cambió.</summary>
    public bool RecalcularInstante()
    {
        var instante = new FechaHoraLocal(HoraEstimadaLocal, ZonaHoraria).Instante();
        if (instante == HoraEstimada)
            return false;
        HoraEstimada = instante;
        return true;
    }

    public void RegistrarLlegada(DateTimeOffset horaReal)
    {
        if (HoraReal is not null)
            throw new InvalidOperationException("La llegada a la parada ya fue registrada.");
        HoraReal = horaReal;
    }
}
