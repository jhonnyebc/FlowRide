namespace FlowRide.Domain.Assignment.Configuration;

public sealed class AssignmentConfiguration
{
    public AssignmentConfiguration(string tenantId, int version, IEnumerable<RuleSetting> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(rules);

        TenantId = tenantId;
        Version = version;
        Rules = rules.ToArray();
    }

    public string TenantId { get; }
    public int Version { get; }
    public IReadOnlyList<RuleSetting> Rules { get; }
}
