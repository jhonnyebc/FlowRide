namespace FlowRide.Domain.Entidades;

/// <summary>Servicio recurrente o programado de una empresa cliente (RF-08).</summary>
public sealed class Itinerario
{
    public Itinerario(
        Guid id,
        Guid empresaClienteId,
        string codigoServicio,
        DateOnly fecha,
        string origen,
        string destino,
        TimeOnly horaSalida,
        TimeOnly horaLlegada)
    {
        Id = Guardas.Id(id, "El itinerario requiere un Id.", nameof(id));
        EmpresaClienteId = Guardas.Id(empresaClienteId, "El itinerario requiere una empresa cliente.", nameof(empresaClienteId));
        CodigoServicio = Guardas.Texto(codigoServicio, nameof(codigoServicio));
        Fecha = fecha;
        Origen = Guardas.Texto(origen, nameof(origen));
        Destino = Guardas.Texto(destino, nameof(destino));
        HoraSalida = horaSalida;
        HoraLlegada = horaLlegada;
    }

    public Guid Id { get; }
    public Guid EmpresaClienteId { get; }
    public string CodigoServicio { get; }
    public DateOnly Fecha { get; }
    public string Origen { get; }
    public string Destino { get; }
    public TimeOnly HoraSalida { get; }
    public TimeOnly HoraLlegada { get; }
}
