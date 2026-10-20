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

/// <summary>Ruta del borde de un operador. Sin subdominio se usa el Id del operador; el número de WhatsApp es opcional.</summary>
public sealed class RegistrarRutaOperadorPeticion
{
    public string? TenantId { get; init; }
    public string? Subdominio { get; init; }
    public string? UrlStack { get; init; }
    public string? PhoneNumberId { get; init; }
}
