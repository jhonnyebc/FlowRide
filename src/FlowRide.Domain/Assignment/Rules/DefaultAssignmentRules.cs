using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Priority;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Assignment.Rules.TieBreaking;

namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Catálogo inicial de reglas (ADR-0009). Una regla nueva = una clase nueva registrada aquí o por DI.</summary>
public static class DefaultAssignmentRules
{
    public static IReadOnlyList<IAssignmentRule> Create() =>
    [
        new AvailabilityRule(),
        new ShiftCompatibilityRule(),
        new RegulatoryRestrictionRule(),
        new ServiceCapabilityRule(),
        new CriticalServicePriorityRule(),
        new WorkloadBalanceRule(),
        new StableTieBreaker()
    ];
}
