using FlowRide.Domain.Assignment.Rules;

namespace FlowRide.Domain.Assignment.Results;

/// <summary>Resultado de una regla sobre un candidato, tal como queda en el decision log.</summary>
/// <param name="Passed">Solo reglas eliminatorias.</param>
/// <param name="RawValue">Solo reglas de prioridad y puntuación (0..100).</param>
/// <param name="Weight">Solo reglas de prioridad y puntuación.</param>
/// <param name="WeightedValue">RawValue × Weight.</param>
public sealed record RuleOutcome(
    string RuleName,
    RuleKind Kind,
    int Order,
    bool? Passed,
    decimal? RawValue,
    decimal? Weight,
    decimal? WeightedValue,
    string Reason);
