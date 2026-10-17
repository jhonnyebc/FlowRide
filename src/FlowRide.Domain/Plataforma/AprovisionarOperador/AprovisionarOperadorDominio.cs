namespace FlowRide.Domain.Plataforma.AprovisionarOperador;

/// <summary>Datos del caso de uso de aprovisionamiento de un operador (RF-03), ya validados en su forma.</summary>
public sealed record AprovisionarOperadorDominio(string TenantId, string RazonSocial, string Nit, string CorreoContacto);
