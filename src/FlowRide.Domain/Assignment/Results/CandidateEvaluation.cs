namespace FlowRide.Domain.Assignment.Results;

public sealed record CandidateEvaluation(
    Candidato Candidato,
    bool Eligible,
    string? DiscardReason,
    IReadOnlyList<RuleOutcome> RuleOutcomes,
    decimal PriorityScore,
    decimal Score,
    int? Rank)
{
    public CandidateReference Reference => Candidato.Reference;

    public decimal TotalScore => PriorityScore + Score;
}
