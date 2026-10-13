namespace FlowRide.Domain.Assignment.Results;

/// <param name="TiedCandidates">Candidatos empatados en el primer puesto, en el orden final.</param>
/// <param name="DecidedBy">Desempatador que resolvió el empate entre el ganador y el segundo.</param>
/// <param name="Criterion">Criterio concreto del desempatador que marcó la diferencia.</param>
public sealed record TieBreakRecord(
    IReadOnlyList<CandidateReference> TiedCandidates,
    string DecidedBy,
    string Criterion,
    CandidateReference Winner);
