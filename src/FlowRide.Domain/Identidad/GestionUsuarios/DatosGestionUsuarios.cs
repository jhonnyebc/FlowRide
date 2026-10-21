namespace FlowRide.Domain.Identidad.GestionUsuarios;

// Datos de entrada de los casos de uso de gestión de usuarios del Panel Web (RF-01, RF-02).
// ActorId es el usuario interno que ejecuta la acción; null cuando la ejecuta la plataforma (aprovisionamiento).

public sealed record CrearUsuarioDominio(
    string Nombre,
    string Correo,
    string TelefonoWhatsApp,
    string Rol,
    Guid? ActorId);

public sealed record CambiarEstadoUsuarioDominio(Guid UsuarioId, bool Activo, Guid? ActorId);

public sealed record CambiarRolUsuarioDominio(Guid UsuarioId, string Rol, Guid? ActorId);

/// <summary>Nuevos datos de contacto de un usuario del Panel Web (RF-24); el correo es también su usuario de inicio de sesión.</summary>
public sealed record ActualizarUsuarioDominio(Guid UsuarioId, string Nombre, string Correo, string TelefonoWhatsApp, Guid? ActorId);

/// <summary>Identificador del usuario en el proveedor OIDC (claim <c>sub</c>) que llega en un token ya validado.</summary>
public sealed record ResolverIdentidadDominio(string IdentidadExternaId);

/// <summary>Filtro opcional por nombre de rol; null devuelve todos los usuarios.</summary>
public sealed record ConsultarUsuariosDominio(string? Rol);
