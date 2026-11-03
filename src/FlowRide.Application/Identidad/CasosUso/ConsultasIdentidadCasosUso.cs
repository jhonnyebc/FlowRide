using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Identidad.Repositorios;

namespace FlowRide.Application.Identidad.CasosUso;

/// <summary>Usuarios del operador, opcionalmente filtrados por rol, en orden por nombre.</summary>
public sealed class ConsultarUsuariosCasoUso(IUsuarioRepositorio usuarios)
    : ICasoUsoConRetorno<ConsultarUsuariosDominio, IReadOnlyList<Usuario>>
{
    public async Task<IReadOnlyList<Usuario>> EjecutarAsync(ConsultarUsuariosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var todos = await usuarios.ConsultarTodosAsync(ct);
        if (string.IsNullOrWhiteSpace(datos.Rol))
            return todos;

        var rol = RolesSistema.PorNombre(datos.Rol);
        return rol is null ? [] : todos.Where(usuario => usuario.RolId == rol.Id).ToList();
    }
}

/// <summary>
/// Mapeo del <c>IdentityProviderAdapter</c> de ADR-0003: identificador externo → usuario interno. Devuelve null si
/// el usuario no existe o está inactivo; en ese caso la petición se rechaza aunque el token siga vigente.
/// Se consulta en cada petición, sin caché, para que la revocación sea inmediata (ADM-SEG-01).
/// </summary>
public sealed class ResolverIdentidadCasoUso(IUsuarioRepositorio usuarios)
    : ICasoUsoConRetorno<ResolverIdentidadDominio, Usuario?>
{
    public async Task<Usuario?> EjecutarAsync(ResolverIdentidadDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (string.IsNullOrWhiteSpace(datos.IdentidadExternaId))
            return null;

        var usuario = await usuarios.ConsultarPorIdentidadExternaAsync(datos.IdentidadExternaId, ct);
        return usuario is { Activo: true } ? usuario : null;
    }
}
