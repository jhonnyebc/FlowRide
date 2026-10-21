using System.Text.Json.Serialization;

namespace FlowRide.Domain.Assignment.Rules;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuleKind
{
    Filtering,
    Priority,
    Scoring,
    TieBreak
}
