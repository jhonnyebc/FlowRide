namespace FlowRide.Domain.Assignment.Results;

public sealed record AssignmentResult(
    AssignmentOutcome Outcome,
    Candidato? Selected,
    string Reason,
    AssignmentDecisionLog DecisionLog)
{
    public bool IsAssigned => Outcome == AssignmentOutcome.Assigned;
}
