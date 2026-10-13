namespace FlowRide.Domain.Entidades;

public sealed class Vehiculo
{
    public Vehiculo(
        Guid id,
        string placa,
        int capacidad,
        DateOnly vencimientoRtm,
        DateOnly vencimientoSoat,
        bool activo = true)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El vehículo requiere un Id.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(placa);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacidad);

        Id = id;
        Placa = placa;
        Capacidad = capacidad;
        VencimientoRtm = vencimientoRtm;
        VencimientoSoat = vencimientoSoat;
        Activo = activo;
    }

    public Guid Id { get; }
    public string Placa { get; }
    public int Capacidad { get; }
    public DateOnly VencimientoRtm { get; }
    public DateOnly VencimientoSoat { get; }
    public bool Activo { get; }

    public bool AdmiteCapacidad(int pasajeros) => Capacidad >= pasajeros;

    public bool DocumentacionVigenteEn(DateOnly fecha) =>
        VencimientoRtm >= fecha && VencimientoSoat >= fecha;
}
