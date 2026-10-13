using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Assignment.Rules.Priority;

/// <summary>
/// En una solicitud crítica (urgente) suma puntos a los conductores que ya están en un turno activo,
/// porque pueden atenderla de inmediato. Una solicitud no crítica no aporta prioridad.
/// </summary>
public sealed class CriticalServicePriorityRule : IPriorityRule
{
    public const string RuleName = nameof(CriticalServicePriorityRule);

    public string Name => RuleName;

    public RuleScore Evaluate(AssignmentContext context, Candidato candidato)
    {
        if (!context.Solicitud.Urgente)
            return new RuleScore(RuleScore.Min, "Solicitud no crítica: sin prioridad adicional.");

        return candidato.Turno.Estado == EstadoTurno.Activo
            ? new RuleScore(RuleScore.Max, "Solicitud crítica y conductor en turno activo.")
            : new RuleScore(RuleScore.Min, "Solicitud crítica, pero el turno del conductor aún no está activo.");
    }
}
