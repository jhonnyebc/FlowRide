namespace FlowRide.Domain.Assignment.Rules;

public sealed record FilterResult(bool Passed, string Reason)
{
    public static FilterResult Pass(string reason) => new(true, reason);

    public static FilterResult Fail(string reason) => new(false, reason);
}
