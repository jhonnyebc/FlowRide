namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Regla de puntuación: valora al candidato (0..100); el motor pondera con el peso configurado.</summary>
public interface IScoringRule : IAssignmentRule
{
    RuleKind IAssignmentRule.Kind => RuleKind.Scoring;

    RuleScore Score(AssignmentContext context, Candidato candidato);
}
