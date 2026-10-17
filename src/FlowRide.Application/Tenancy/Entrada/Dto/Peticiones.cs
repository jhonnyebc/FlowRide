namespace FlowRide.Application.Tenancy.Entrada.Dto;

/// <summary>Petición de aprovisionamiento de un operador (RF-03). Todos los campos son obligatorios.</summary>
public sealed class AprovisionarOperadorPeticion
{
    public string? TenantId { get; init; }
    public string? RazonSocial { get; init; }
    public string? Nit { get; init; }
    public string? CorreoContacto { get; init; }
}

/// <summary>Petición de migración: un operador concreto o, sin <see cref="TenantId"/>, todos los no suspendidos.</summary>
public sealed class MigrarSilosPeticion
{
    public string? TenantId { get; init; }
}
