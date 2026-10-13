namespace FlowRide.Domain.Assignment.Rules.Scoring;

/// <summary>Favorece a quien tiene menos asignaciones recientes: puntaje = 100 / (1 + asignaciones recientes).</summary>
public sealed class WorkloadBalanceRule : IScoringRule
{
    public const string RuleName = nameof(WorkloadBalanceRule);

    public string Name => RuleName;

    public RuleScore Score(AssignmentContext context, Candidato candidato)
    {
        var recientes = candidato.AsignacionesRecientes;
        return new RuleScore(RuleScore.Max / (1 + recientes), $"{recientes} asignaciones recientes.");
    }
}
