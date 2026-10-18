using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Assignment;

/// <summary>Combinación conductor + vehículo + turno que el motor evalúa para una solicitud.</summary>
public sealed class Candidato
{
    public Candidato(
        Conductor conductor,
        Vehiculo vehiculo,
        Turno turno,
        int asignacionesRecientes = 0,
        bool conductorOcupado = false,
        bool vehiculoOcupado = false)
    {
        ArgumentNullException.ThrowIfNull(conductor);
        ArgumentNullException.ThrowIfNull(vehiculo);
        ArgumentNullException.ThrowIfNull(turno);
        ArgumentOutOfRangeException.ThrowIfNegative(asignacionesRecientes);
        if (turno.ConductorId != conductor.Id)
            throw new ArgumentException("El turno no pertenece al conductor del candidato.", nameof(turno));

        Conductor = conductor;
        Vehiculo = vehiculo;
        Turno = turno;
        AsignacionesRecientes = asignacionesRecientes;
        ConductorOcupado = conductorOcupado;
        VehiculoOcupado = vehiculoOcupado;
        Reference = new CandidateReference(conductor.Id, vehiculo.Id, turno.Id);
    }

    public Conductor Conductor { get; }
    public Vehiculo Vehiculo { get; }
    public Turno Turno { get; }

    /// <summary>Asignaciones del conductor en la ventana reciente definida por quien construye el candidato.</summary>
    public int AsignacionesRecientes { get; }

    /// <summary>El conductor ya tiene una asignación vigente que se solapa con la franja de la solicitud.</summary>
    public bool ConductorOcupado { get; }

    /// <summary>El vehículo ya tiene una asignación vigente que se solapa con la franja de la solicitud.</summary>
    public bool VehiculoOcupado { get; }

    public DateTimeOffset? UltimaAsignacion => Conductor.FechaUltimaAsignacion;

    public CandidateReference Reference { get; }
}
