using FlowRide.Domain.Assignment.Results;

namespace FlowRide.Domain.Assignment.Rules.TieBreaking;

/// <summary>
/// Desempate estable: 1) menos asignaciones recientes, 2) mayor tiempo desde la última asignación
/// (nunca asignado gana), 3) Id del conductor.
/// </summary>
public sealed class StableTieBreaker : ITieBreaker
{
    public const string RuleName = nameof(StableTieBreaker);

    public const string ByRecentAssignments = "Menos asignaciones recientes";
    public const string ByLongestIdle = "Mayor tiempo desde la última asignación";
    public const string ByConductorId = "Id del conductor";

    public string Name => RuleName;

    public TieBreakComparison Compare(CandidateEvaluation a, CandidateEvaluation b)
    {
        var byRecent = a.Candidato.AsignacionesRecientes.CompareTo(b.Candidato.AsignacionesRecientes);
        if (byRecent != 0) return new TieBreakComparison(byRecent, ByRecentAssignments);

        var byIdle = CompareLastAssignment(a.Candidato.UltimaAsignacion, b.Candidato.UltimaAsignacion);
        if (byIdle != 0) return new TieBreakComparison(byIdle, ByLongestIdle);

        var byId = a.Candidato.Conductor.Id.CompareTo(b.Candidato.Conductor.Id);
        return byId != 0 ? new TieBreakComparison(byId, ByConductorId) : TieBreakComparison.Undecided;
    }

    // La asignación más antigua (o ninguna) va primero.
    private static int CompareLastAssignment(DateTimeOffset? a, DateTimeOffset? b) => (a, b) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        _ => a.Value.CompareTo(b.Value)
    };
}
