using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Identidad;

/// <summary>
/// Los cuatro roles de FlowRide (RBAC, ADR-0003). Son fijos: cada Silo los recibe con la migración y con estos Id,
/// así que el código puede referirse a ellos sin consultar la base. El rol vive en FlowRide, no en el token.
/// </summary>
public static class RolesSistema
{
    public const string Administrador = "Administrador";
    public const string OperadorLogistico = "OperadorLogistico";
    public const string Pasajero = "Pasajero";
    public const string Conductor = "Conductor";

    public static readonly Guid AdministradorId = new("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid OperadorLogisticoId = new("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid PasajeroId = new("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid ConductorId = new("a1000000-0000-0000-0000-000000000004");

    public static IReadOnlyList<Rol> Todos { get; } =
    [
        new(AdministradorId, Administrador, "Administrador del operador: gestiona usuarios, recursos y parámetros."),
        new(OperadorLogisticoId, OperadorLogistico, "Operador logístico: supervisa solicitudes, asignaciones y viajes."),
        new(PasajeroId, Pasajero, "Pasajero: solicita y sigue sus viajes por WhatsApp."),
        new(ConductorId, Conductor, "Conductor: recibe y ejecuta asignaciones por WhatsApp.")
    ];

    /// <summary>Roles que inician sesión en el Panel Web con el proveedor OIDC.</summary>
    public static IReadOnlyList<string> DelPanel { get; } = [Administrador, OperadorLogistico];

    public static Rol? PorNombre(string? nombre) =>
        Todos.FirstOrDefault(rol => string.Equals(rol.Nombre, nombre?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string NombrePorId(Guid rolId) =>
        Todos.FirstOrDefault(rol => rol.Id == rolId)?.Nombre
        ?? throw new InvalidOperationException($"El rol {rolId} no es un rol del sistema.");

    public static bool EsDelPanel(Guid rolId) => rolId == AdministradorId || rolId == OperadorLogisticoId;

    /// <summary>
    /// Cuentas privilegiadas que inician sesión con doble factor (TOTP), decisión del 2026-10-20 tras los videos del
    /// profesor (OWASP, autenticación rota): el Administrador gestiona usuarios, roles, parámetros y plantillas.
    /// </summary>
    public static bool ExigeDobleFactor(Guid rolId) => rolId == AdministradorId;
}
