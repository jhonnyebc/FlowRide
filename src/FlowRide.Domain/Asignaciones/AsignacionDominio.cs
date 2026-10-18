using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Asignaciones;

/// <summary>Lo que el caso de uso sabe de la operación en la franja de la solicitud, para armar candidatos.</summary>
public sealed record AgendaOperacion(
    IReadOnlySet<Guid> ConductoresOcupados,
    IReadOnlySet<Guid> VehiculosOcupados,
    IReadOnlyDictionary<Guid, int> AsignacionesRecientes);

/// <summary>Reglas de la asignación que no son del motor: cómo se arman los candidatos y cómo se valida uno manual.</summary>
public static class AsignacionDominio
{
    /// <summary>
    /// Candidatos de una solicitud (decisión del 2026-10-18): cada conductor con un turno que cubre la franja, con su
    /// vehículo habitual o, si no tiene, con cada vehículo activo sin conductor habitual. La ocupación se marca en el
    /// candidato para que la regla de disponibilidad lo descarte con su motivo en el decision log.
    /// </summary>
    public static IReadOnlyList<Candidato> Candidatos(
        SolicitudViaje solicitud, IEnumerable<Conductor> conductores, IReadOnlyList<Vehiculo> vehiculos, AgendaOperacion agenda)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(conductores);
        ArgumentNullException.ThrowIfNull(vehiculos);
        ArgumentNullException.ThrowIfNull(agenda);

        var franja = solicitud.Franja();
        var libres = vehiculos.Where(v => v.Activo && v.ConductorHabitualId is null).OrderBy(v => v.Id).ToList();
        var candidatos = new List<Candidato>();
        foreach (var conductor in conductores.OrderBy(c => c.Id))
        {
            if (TurnoQueCubre(conductor, franja) is not { } turno)
                continue;

            var habitual = vehiculos.FirstOrDefault(v => v.ConductorHabitualId == conductor.Id);
            foreach (var vehiculo in habitual is null ? libres : [habitual])
                candidatos.Add(Candidato(conductor, vehiculo, turno, agenda));
        }
        return candidatos;
    }

    /// <summary>El candidato que propone el Operador logístico (RF-12), con la misma información que uno automático.</summary>
    public static Candidato CandidatoManual(SolicitudViaje solicitud, Conductor conductor, Vehiculo vehiculo, AgendaOperacion agenda)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(conductor);
        var turno = TurnoQueCubre(conductor, solicitud.Franja())
                    ?? throw new FlowRideExcepcion("El conductor no tiene un turno vigente que cubra el horario de la solicitud.");
        return Candidato(conductor, vehiculo, turno, agenda);
    }

    /// <summary>Un candidato manual debe pasar las reglas eliminatorias del motor; si no, se explica por qué.</summary>
    public static void ValidarCandidatoManual(AssignmentResult resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        if (resultado.IsAssigned)
            return;

        var motivo = resultado.DecisionLog.Discarded.FirstOrDefault()?.Reason ?? resultado.Reason;
        throw new FlowRideExcepcion($"El conductor y el vehículo propuestos no cumplen las reglas obligatorias: {motivo}");
    }

    private static Turno? TurnoQueCubre(Conductor conductor, FranjaHoraria franja) =>
        conductor.Turnos.Where(turno => turno.Cubre(franja)).OrderBy(turno => turno.Inicio).FirstOrDefault()
        ?? conductor.Turnos.Where(turno => turno.Cubre(franja.Inicio)).OrderBy(turno => turno.Inicio).FirstOrDefault();

    private static Candidato Candidato(Conductor conductor, Vehiculo vehiculo, Turno turno, AgendaOperacion agenda) =>
        new(conductor, vehiculo, turno, agenda.AsignacionesRecientes.GetValueOrDefault(conductor.Id),
            agenda.ConductoresOcupados.Contains(conductor.Id), agenda.VehiculosOcupados.Contains(vehiculo.Id));
}
