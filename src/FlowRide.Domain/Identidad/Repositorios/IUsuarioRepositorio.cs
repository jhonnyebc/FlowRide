using FlowRide.Domain.Comun.Repositorios;
using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Identidad.Repositorios;

/// <summary>
/// Usuarios del Silo. Los cambios quedan pendientes hasta que el caso de uso confirma la unidad de trabajo, para que
/// el cambio y su evento de auditoría se guarden juntos.
/// </summary>
public interface IUsuarioRepositorio
    : ICrearRepositorio<Usuario>, IConsultarRepositorio<Guid, Usuario>, IModificarRepositorio<Guid, Usuario>
{
    Task<Usuario?> ConsultarPorCorreoAsync(string correo, CancellationToken ct);

    Task<Usuario?> ConsultarPorTelefonoAsync(string telefonoWhatsApp, CancellationToken ct);

    Task<Usuario?> ConsultarPorIdentidadExternaAsync(string identidadExternaId, CancellationToken ct);

    /// <summary>Usuarios activos con el rol (p. ej. los administradores que reciben las alertas críticas por correo).</summary>
    Task<IReadOnlyList<Usuario>> ConsultarActivosPorRolAsync(Guid rolId, CancellationToken ct);
}
