namespace FlowRide.Domain.Assignment.Rules;

public sealed record TieBreakComparison(int Result, string Criterion)
{
    public static TieBreakComparison Undecided { get; } = new(0, "Sin diferencia");
}
