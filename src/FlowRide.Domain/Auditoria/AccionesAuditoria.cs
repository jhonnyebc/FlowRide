namespace FlowRide.Domain.Auditoria;

/// <summary>Acciones sensibles que quedan en la auditoría legal (ADR-0011), con el formato <c>recurso.accion</c>.</summary>
public static class AccionesAuditoria
{
    public const string RecursoUsuario = "usuario";

    public const string UsuarioCrear = "usuario.crear";
    public const string UsuarioActivar = "usuario.activar";
    public const string UsuarioDesactivar = "usuario.desactivar";
    public const string UsuarioCambiarRol = "usuario.cambiar_rol";
}
