using System.ComponentModel.DataAnnotations;
using FlowRide.Domain.Plataforma;

namespace FlowRide.Infrastructure.Identidad;

/// <summary>Configuración (no secreta) del proveedor OIDC del stack del operador (ADR-0003).</summary>
public sealed class IdentidadOptions
{
    public const string Seccion = "FlowRide:Identidad";

    /// <summary>URL base de Keycloak (sin <c>/realms</c>). El realm se deriva del TenantId del stack.</summary>
    [Required(ErrorMessage = "Falta la configuración 'FlowRide:Identidad:UrlKeycloak'.")]
    [Url(ErrorMessage = "'FlowRide:Identidad:UrlKeycloak' debe ser una URL absoluta.")]
    public string UrlKeycloak { get; set; } = string.Empty;

    /// <summary>Origen del Panel Web: destino del enlace del correo para definir la contraseña.</summary>
    [Url(ErrorMessage = "'FlowRide:Identidad:UrlPanel' debe ser una URL absoluta.")]
    public string UrlPanel { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Origen del Panel Web que sirve el borde para cada operador, con <c>{operador}</c> en lugar de su id (en local,
    /// <c>https://{operador}.dev.localhost:9443</c>). Si está definido, el realm también lo acepta como destino de regreso.
    /// </summary>
    public string? UrlPanelOperador { get; set; }

    /// <summary>Origen del Panel Web servido por el borde para un operador, o null si no está configurado.</summary>
    public string? UrlPanelDe(string tenantId) =>
        string.IsNullOrWhiteSpace(UrlPanelOperador) ? null : UrlPanelOperador.Replace("{operador}", tenantId, StringComparison.Ordinal).TrimEnd('/');

    /// <summary>Exige HTTPS para leer los metadatos OIDC. Solo se apaga en desarrollo local.</summary>
    public bool RequiereHttps { get; set; } = true;

    /// <summary>Minutos desde el último inicio de sesión dentro de los que se permiten las acciones sensibles (RFC 9470).</summary>
    public int MinutosAutenticacionReciente { get; set; } = 5;

    public string Autoridad(string nombreRealm) => $"{UrlKeycloak.TrimEnd('/')}/realms/{nombreRealm}";

    /// <summary>Emisor de los tokens del operador: su realm, derivado de su TenantId.</summary>
    public string AutoridadDelOperador(string tenantId) => Autoridad(Operador.NombreRealmDe(tenantId));
}

/// <summary>Nombres fijos de la plantilla del realm (<c>plantilla-realm.json</c>).</summary>
public static class ConstantesKeycloak
{
    public const string ClientePanel = "flowride-panel";
    public const string ClienteServicio = "flowride-api";

    /// <summary>Audiencia que el realm agrega a los tokens del Panel Web y que la API exige.</summary>
    public const string Audiencia = "flowride-api";

    /// <summary>Claim con el operador (ADR-0002): debe coincidir con el TenantId del stack.</summary>
    public const string ClaimTenant = "tenant_id";

    /// <summary>Permisos del cliente de servicio sobre las cuentas de su propio realm.</summary>
    public static readonly string[] RolesCuentaServicio = ["manage-users", "view-users", "query-users"];
}
