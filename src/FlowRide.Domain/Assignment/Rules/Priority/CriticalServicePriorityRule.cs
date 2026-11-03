namespace FlowRide.Domain.Assignment.Rules.Priority;

/// <summary>
/// En una solicitud crítica (urgente) suma puntos a los conductores marcados como compatibles con servicio crítico
/// (ADR-0009, escenario 3). Una solicitud no crítica no aporta prioridad.
/// </summary>
public sealed class CriticalServicePriorityRule : IPriorityRule
{
    public const string RuleName = nameof(CriticalServicePriorityRule);

    public string Name => RuleName;

    /// <summary>v2: la compatibilidad es una marca del conductor; v1 usaba "turno activo" como aproximación.</summary>
    public int Version => 2;

    public RuleScore Evaluate(AssignmentContext context, Candidato candidato)
    {
        if (!context.Solicitud.Urgente)
            return new RuleScore(RuleScore.Min, "Solicitud no crítica: sin prioridad adicional.");

        return candidato.Conductor.AptoServicioCritico
            ? new RuleScore(RuleScore.Max, "Solicitud crítica y conductor compatible con servicio crítico.")
            : new RuleScore(RuleScore.Min, "Solicitud crítica, pero el conductor no está marcado como compatible con servicio crítico.");
    }
}
