using System.ComponentModel.DataAnnotations;

namespace FlowRide.Infrastructure.Plataforma;

/// <summary>
/// Identidad del stack: cada contenedor de aplicación sirve a un único operador (ADR-0002, ADR-0010).
/// </summary>
public sealed class SiloOptions
{
    public const string Seccion = "FlowRide";

    /// <summary>Identificador estable del operador (tenant), en minúsculas y con guiones.</summary>
    [Required(ErrorMessage = "Falta la configuración 'FlowRide:TenantId'.")]
    [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$",
        ErrorMessage = "'FlowRide:TenantId' debe estar en minúsculas, con letras, números y guiones.")]
    public string TenantId { get; set; } = string.Empty;
}
