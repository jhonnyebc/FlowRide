namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Regla de prioridad: aporta puntos (0..100) que el motor pondera con el peso configurado.</summary>
public interface IPriorityRule : IAssignmentRule
{
    RuleKind IAssignmentRule.Kind => RuleKind.Priority;

    RuleScore Evaluate(AssignmentContext context, Candidato candidato);
}
