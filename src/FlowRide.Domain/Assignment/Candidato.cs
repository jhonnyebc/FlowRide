using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Assignment;

/// <summary>Combinación conductor + vehículo + turno que el motor evalúa para una solicitud.</summary>
public sealed class Candidato
{
    public Candidato(Conductor conductor, Vehiculo vehiculo, Turno turno, int asignacionesRecientes = 0)
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
        Reference = new CandidateReference(conductor.Id, vehiculo.Id, turno.Id);
    }

    public Conductor Conductor { get; }
    public Vehiculo Vehiculo { get; }
    public Turno Turno { get; }

    /// <summary>Asignaciones del conductor en la ventana reciente definida por quien construye el candidato.</summary>
    public int AsignacionesRecientes { get; }

    public DateTimeOffset? UltimaAsignacion => Conductor.FechaUltimaAsignacion;

    public CandidateReference Reference { get; }
}
