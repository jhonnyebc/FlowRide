namespace FlowRide.Domain.Plataforma.MigrarSilos;

/// <summary>Datos del caso de uso de migración: un operador concreto o, si <see cref="TenantId"/> es null, todos.</summary>
public sealed record MigrarSilosDominio(string? TenantId)
{
    public static MigrarSilosDominio Todos { get; } = new((string?)null);

    public bool EsTodos => TenantId is null;
}
