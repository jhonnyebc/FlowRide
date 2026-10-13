namespace FlowRide.Domain.Assignment.Results;

public sealed record DiscardedCandidate(CandidateReference Candidate, string Reason);

public sealed record CandidateScore(
    CandidateReference Candidate,
    decimal PriorityScore,
    decimal Score,
    decimal TotalScore,
    int Rank);

/// <summary>Todo lo necesario para reconstruir una decisión del motor (ADR-0009).</summary>
/// <param name="Evaluations">Resultado de cada regla por candidato, en el orden de evaluación.</param>
public sealed record AssignmentDecisionLog(
    Guid SolicitudId,
    string TenantId,
    int ConfigurationVersion,
    AssignmentOutcome Outcome,
    CandidateReference? Selected,
    string Reason,
    IReadOnlyList<CandidateReference> InitialCandidates,
    IReadOnlyList<CandidateEvaluation> Evaluations,
    IReadOnlyList<DiscardedCandidate> Discarded,
    IReadOnlyList<CandidateScore> Scores,
    TieBreakRecord? TieBreak);
