using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;

namespace FlowRide.Domain.Assignment;

/// <summary>
/// Orquesta las reglas configuradas del tenant en un orden fijo:
/// eliminatorias → prioridad → puntuación → ranking → desempate.
/// Lógica pura y determinista: misma entrada + misma configuración ⇒ mismo resultado.
/// </summary>
public sealed class AssignmentEngine
{
    private const string EngineFallbackName = nameof(AssignmentEngine);
    private const string EngineFallbackCriterion = "Orden estable por identificadores del candidato";

    private readonly IReadOnlyList<IAssignmentRule> _rules;
    private readonly IReadOnlyDictionary<string, IAssignmentRule> _catalog;
    private readonly ConfigurationValidator _validator = new();

    public AssignmentEngine(IEnumerable<IAssignmentRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        var catalog = new Dictionary<string, IAssignmentRule>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (!catalog.TryAdd(rule.Name, rule))
                throw new ArgumentException($"La regla '{rule.Name}' está registrada más de una vez en el catálogo.", nameof(rules));
        }

        _catalog = catalog;
        _rules = catalog.Values.ToArray();
    }

    public AssignmentResult Decide(AssignmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var configuration = context.Configuration;
        var candidatos = context.Candidatos.OrderBy(c => c.Reference).ToArray();
        var initial = candidatos.Select(c => c.Reference).ToArray();

        var validation = _validator.Validate(configuration, _rules);
        if (!validation.IsValid)
        {
            var reason = "Configuración inválida: " + string.Join(" ", validation.Errors);
            return NotAssigned(context, AssignmentOutcome.InvalidConfiguration, reason, initial, [], null);
        }

        var active = configuration.Rules
            .Where(setting => setting.Enabled)
            .OrderBy(setting => setting.Order)
            .ThenBy(setting => setting.Name, StringComparer.Ordinal)
            .Select(setting => new ActiveRule(setting, _catalog[setting.Name]))
            .ToArray();
        var filters = Of<IFilteringRule>(active);
        var priorities = Of<IPriorityRule>(active);
        var scorers = Of<IScoringRule>(active);
        var tieBreakers = Of<ITieBreaker>(active);

        var evaluations = candidatos
            .Select(c => ApplyFiltering(context, c, filters))
            .Select(e => e.Eligible ? ApplyPriorityAndScoring(context, e, priorities, scorers) : e)
            .ToArray();

        var ranked = Rank(evaluations.Where(e => e.Eligible), tieBreakers);
        var byReference = ranked.ToDictionary(e => e.Reference);
        evaluations = evaluations.Select(e => byReference.GetValueOrDefault(e.Reference, e)).ToArray();

        if (ranked.Count == 0)
        {
            var reason = candidatos.Length == 0
                ? "No se recibieron candidatos para la solicitud."
                : "Ningún candidato superó las reglas eliminatorias.";
            return NotAssigned(context, AssignmentOutcome.NoCandidates, reason, initial, evaluations, null);
        }

        var winner = ranked[0];
        var tieBreak = BreakTies(ranked, tieBreakers);
        var selectionReason = tieBreak is null
            ? $"Mayor puntaje total ({winner.TotalScore})."
            : $"Empate en puntaje total ({winner.TotalScore}) resuelto por {tieBreak.DecidedBy}: {tieBreak.Criterion}.";

        var log = BuildLog(context, AssignmentOutcome.Assigned, winner.Reference, selectionReason, initial, evaluations, tieBreak);
        return new AssignmentResult(AssignmentOutcome.Assigned, winner.Candidato, selectionReason, log);
    }

    private static CandidateEvaluation ApplyFiltering(
        AssignmentContext context,
        Candidato candidato,
        IReadOnlyList<ActiveRule<IFilteringRule>> filters)
    {
        var outcomes = new List<RuleOutcome>(filters.Count);
        string? discardReason = null;

        // Se evalúan todas las eliminatorias para que el log muestre el estado completo del candidato.
        foreach (var (setting, rule) in filters)
        {
            var result = rule.Evaluate(context, candidato);
            outcomes.Add(new RuleOutcome(rule.Name, rule.Kind, setting.Order, result.Passed, null, null, null, result.Reason, rule.Version));
            if (!result.Passed)
                discardReason ??= $"{rule.Name}: {result.Reason}";
        }

        return new CandidateEvaluation(candidato, discardReason is null, discardReason, outcomes, 0m, 0m, null);
    }

    private static CandidateEvaluation ApplyPriorityAndScoring(
        AssignmentContext context,
        CandidateEvaluation evaluation,
        IReadOnlyList<ActiveRule<IPriorityRule>> priorities,
        IReadOnlyList<ActiveRule<IScoringRule>> scorers)
    {
        var outcomes = new List<RuleOutcome>(evaluation.RuleOutcomes);

        var priority = 0m;
        foreach (var (setting, rule) in priorities)
            priority += Weigh(outcomes, setting, rule, rule.Evaluate(context, evaluation.Candidato));

        var score = 0m;
        foreach (var (setting, rule) in scorers)
            score += Weigh(outcomes, setting, rule, rule.Score(context, evaluation.Candidato));

        return evaluation with { RuleOutcomes = outcomes, PriorityScore = priority, Score = score };
    }

    private static decimal Weigh(List<RuleOutcome> outcomes, RuleSetting setting, IAssignmentRule rule, RuleScore raw)
    {
        var weighted = Math.Round(raw.Value * setting.Weight, 4, MidpointRounding.ToEven);
        outcomes.Add(new RuleOutcome(rule.Name, rule.Kind, setting.Order, null, raw.Value, setting.Weight, weighted, raw.Reason, rule.Version));
        return weighted;
    }

    private static IReadOnlyList<CandidateEvaluation> Rank(
        IEnumerable<CandidateEvaluation> eligible,
        IReadOnlyList<ActiveRule<ITieBreaker>> tieBreakers)
    {
        var comparer = Comparer<CandidateEvaluation>.Create((a, b) =>
        {
            var byScore = b.TotalScore.CompareTo(a.TotalScore);
            if (byScore != 0) return byScore;

            foreach (var (_, tieBreaker) in tieBreakers)
            {
                var result = tieBreaker.Compare(a, b).Result;
                if (result != 0) return Math.Sign(result);
            }

            // Garantiza un orden total aunque no haya desempatadores habilitados.
            return a.Reference.CompareTo(b.Reference);
        });

        return eligible.Order(comparer).Select((e, index) => e with { Rank = index + 1 }).ToArray();
    }

    private static TieBreakRecord? BreakTies(
        IReadOnlyList<CandidateEvaluation> ranked,
        IReadOnlyList<ActiveRule<ITieBreaker>> tieBreakers)
    {
        var tied = ranked.TakeWhile(e => e.TotalScore == ranked[0].TotalScore).ToArray();
        if (tied.Length < 2) return null;

        var (winner, runnerUp) = (tied[0], tied[1]);
        var (decidedBy, criterion) = (EngineFallbackName, EngineFallbackCriterion);
        foreach (var (_, tieBreaker) in tieBreakers)
        {
            var comparison = tieBreaker.Compare(winner, runnerUp);
            if (comparison.Result != 0)
            {
                (decidedBy, criterion) = (tieBreaker.Name, comparison.Criterion);
                break;
            }
        }

        return new TieBreakRecord(tied.Select(e => e.Reference).ToArray(), decidedBy, criterion, winner.Reference);
    }

    private static AssignmentResult NotAssigned(
        AssignmentContext context,
        AssignmentOutcome outcome,
        string reason,
        IReadOnlyList<CandidateReference> initial,
        IReadOnlyList<CandidateEvaluation> evaluations,
        TieBreakRecord? tieBreak)
    {
        var log = BuildLog(context, outcome, null, reason, initial, evaluations, tieBreak);
        return new AssignmentResult(outcome, null, reason, log);
    }

    private static AssignmentDecisionLog BuildLog(
        AssignmentContext context,
        AssignmentOutcome outcome,
        CandidateReference? selected,
        string reason,
        IReadOnlyList<CandidateReference> initial,
        IReadOnlyList<CandidateEvaluation> evaluations,
        TieBreakRecord? tieBreak)
    {
        var discarded = evaluations
            .Where(e => !e.Eligible)
            .Select(e => new DiscardedCandidate(e.Reference, e.DiscardReason!))
            .ToArray();
        var scores = evaluations
            .Where(e => e.Rank is not null)
            .OrderBy(e => e.Rank)
            .Select(e => new CandidateScore(e.Reference, e.PriorityScore, e.Score, e.TotalScore, e.Rank!.Value))
            .ToArray();

        return new AssignmentDecisionLog(
            context.Solicitud.Id,
            context.Configuration.TenantId,
            context.Configuration.Version,
            outcome,
            selected,
            reason,
            initial,
            evaluations,
            discarded,
            scores,
            tieBreak,
            context.EvaluadaEn);
    }

    private static IReadOnlyList<ActiveRule<TRule>> Of<TRule>(IEnumerable<ActiveRule> active)
        where TRule : IAssignmentRule =>
        active
            .Where(a => a.Rule is TRule)
            .Select(a => new ActiveRule<TRule>(a.Setting, (TRule)a.Rule))
            .ToArray();

    private sealed record ActiveRule(RuleSetting Setting, IAssignmentRule Rule);

    private sealed record ActiveRule<TRule>(RuleSetting Setting, TRule Rule) where TRule : IAssignmentRule;
}
