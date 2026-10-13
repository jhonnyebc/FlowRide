namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Regla eliminatoria: el candidato que no la supera queda descartado.</summary>
public interface IFilteringRule : IAssignmentRule
{
    RuleKind IAssignmentRule.Kind => RuleKind.Filtering;

    FilterResult Evaluate(AssignmentContext context, Candidato candidato);
}
