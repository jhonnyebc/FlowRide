namespace FlowRide.Domain.Assignment.Rules;

public sealed record RuleScore
{
    public const decimal Min = 0m;
    public const decimal Max = 100m;

    public RuleScore(decimal value, string reason)
    {
        if (value is < Min or > Max)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"El puntaje de una regla debe estar en [{Min}, {Max}].");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Value = Math.Round(value, 4, MidpointRounding.ToEven);
        Reason = reason;
    }

    public decimal Value { get; }
    public string Reason { get; }
}
