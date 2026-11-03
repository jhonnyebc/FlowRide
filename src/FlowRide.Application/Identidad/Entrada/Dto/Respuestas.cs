namespace FlowRide.Application.Identidad.Entrada.Dto;

public sealed record UsuarioRespuesta(
    Guid Id,
    string Nombre,
    string? Correo,
    string TelefonoWhatsApp,
    string Rol,
    bool Activo,
    DateTimeOffset CreadoEn);

public sealed record AdministradorInicialRespuesta(UsuarioRespuesta Usuario, bool Nuevo, string Mensaje);

/// <summary>Usuario interno detrás de un token válido: lo que la API usa para autorizar (RBAC).</summary>
public sealed record UsuarioAutenticadoRespuesta(Guid Id, string Nombre, string? Correo, string Rol);

/// <summary>Nombres de los roles del Panel Web, para que los adaptadores de infraestructura autoricen sin conocer el dominio.</summary>
public static class RolesPanel
{
    public const string Administrador = Domain.Identidad.RolesSistema.Administrador;
    public const string OperadorLogistico = Domain.Identidad.RolesSistema.OperadorLogistico;
}
