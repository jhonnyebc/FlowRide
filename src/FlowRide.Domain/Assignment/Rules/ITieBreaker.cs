using FlowRide.Domain.Assignment.Results;

namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Criterio de desempate entre candidatos con el mismo puntaje total.</summary>
public interface ITieBreaker : IAssignmentRule
{
    RuleKind IAssignmentRule.Kind => RuleKind.TieBreak;

    /// <summary>Resultado negativo si <paramref name="a"/> va antes que <paramref name="b"/>; cero si no los distingue.</summary>
    TieBreakComparison Compare(CandidateEvaluation a, CandidateEvaluation b);
}
