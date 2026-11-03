namespace FlowRide.Domain.Entidades;

public sealed class Vehiculo
{
    public Vehiculo(
        Guid id,
        string placa,
        string marca,
        string modelo,
        int capacidad,
        DateOnly vencimientoRtm,
        DateOnly vencimientoSoat,
        bool activo = true,
        Guid? conductorHabitualId = null)
    {
        Id = Guardas.Id(id, "El vehículo requiere un Id.", nameof(id));
        Placa = Guardas.Texto(placa, nameof(placa)).ToUpperInvariant();
        Marca = Guardas.Texto(marca, nameof(marca));
        Modelo = Guardas.Texto(modelo, nameof(modelo));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacidad);
        Capacidad = capacidad;
        VencimientoRtm = vencimientoRtm;
        VencimientoSoat = vencimientoSoat;
        Activo = activo;
        AsignarConductorHabitual(conductorHabitualId);
    }

    public Guid Id { get; }
    public string Placa { get; }
    public string Marca { get; private set; }
    public string Modelo { get; private set; }
    public int Capacidad { get; private set; }
    public DateOnly VencimientoRtm { get; private set; }
    public DateOnly VencimientoSoat { get; private set; }
    public bool Activo { get; private set; }

    /// <summary>Conductor que usa el vehículo de forma habitual (0..1, único por vehículo).</summary>
    public Guid? ConductorHabitualId { get; private set; }

    public bool AdmiteCapacidad(int pasajeros) => Capacidad >= pasajeros;

    public bool DocumentacionVigenteEn(DateOnly fecha) =>
        VencimientoRtm >= fecha && VencimientoSoat >= fecha;

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public void RenovarDocumentacion(DateOnly vencimientoRtm, DateOnly vencimientoSoat)
    {
        VencimientoRtm = vencimientoRtm;
        VencimientoSoat = vencimientoSoat;
    }

    public void ActualizarDatos(string marca, string modelo, int capacidad)
    {
        Marca = Guardas.Texto(marca, nameof(marca));
        Modelo = Guardas.Texto(modelo, nameof(modelo));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacidad);
        Capacidad = capacidad;
    }

    public void AsignarConductorHabitual(Guid? conductorId)
    {
        if (conductorId == Guid.Empty)
            throw new ArgumentException("El conductor habitual no puede tener un Id vacío.", nameof(conductorId));
        ConductorHabitualId = conductorId;
    }
}
