using FlowRide.Application.Identidad.Entrada.Dto;

namespace FlowRide.Application.Identidad.Entrada.Puertos;

// actorId: usuario interno autenticado que ejecuta la acción (queda en la auditoría).

public interface ICrearUsuarioPuertoEntrada
{
    Task<UsuarioRespuesta> CrearAsync(CrearUsuarioPeticion peticion, Guid? actorId, CancellationToken ct);
}

public interface IAsegurarAdministradorInicialPuertoEntrada
{
    Task<AdministradorInicialRespuesta> AsegurarAsync(CrearUsuarioPeticion peticion, CancellationToken ct);
}

public interface ICambiarEstadoUsuarioPuertoEntrada
{
    Task<UsuarioRespuesta> CambiarEstadoAsync(Guid usuarioId, bool activo, Guid? actorId, CancellationToken ct);
}

public interface ICambiarRolUsuarioPuertoEntrada
{
    Task<UsuarioRespuesta> CambiarRolAsync(Guid usuarioId, CambiarRolUsuarioPeticion peticion, Guid? actorId, CancellationToken ct);
}

public interface IActualizarUsuarioPuertoEntrada
{
    Task<UsuarioRespuesta> ActualizarAsync(Guid usuarioId, ActualizarUsuarioPeticion peticion, Guid? actorId, CancellationToken ct);
}

public interface IConsultarUsuariosPuertoEntrada
{
    Task<IReadOnlyList<UsuarioRespuesta>> ConsultarAsync(string? rol, CancellationToken ct);
}

public interface IResolverIdentidadPuertoEntrada
{
    /// <summary>Null si el usuario no existe o está inactivo.</summary>
    Task<UsuarioAutenticadoRespuesta?> ResolverAsync(string identidadExternaId, CancellationToken ct);
}
