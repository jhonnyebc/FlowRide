namespace FlowRide.Domain.Assignment.Configuration;

/// <summary>Configuración de una regla del catálogo para un tenant.</summary>
public sealed record RuleSetting
{
    public RuleSetting(string name, bool enabled, int order, decimal weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Enabled = enabled;
        Order = order;
        Weight = weight;
    }

    public string Name { get; }
    public bool Enabled { get; }
    public int Order { get; }

    /// <summary>Peso en [0, 1]. Solo afecta a reglas de prioridad y de puntuación.</summary>
    public decimal Weight { get; }
}
