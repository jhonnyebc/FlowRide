using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Identidad;

/// <summary>
/// Adaptador de salida del repositorio de usuarios sobre el Silo. Crear y modificar dejan el cambio pendiente: lo
/// confirma la unidad de trabajo junto con su evento de auditoría.
/// </summary>
public sealed class UsuarioRepositorioAdaptador(FlowRideDbContext contexto) : IUsuarioRepositorio
{
    public Task CrearAsync(Usuario entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Usuarios.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, Usuario entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (entidad.Id != id)
            throw new ArgumentException($"El Id '{id}' no coincide con el del usuario '{entidad.Id}'.", nameof(id));

        if (contexto.Entry(entidad).State == EntityState.Detached)
            contexto.Usuarios.Update(entidad);
        return Task.CompletedTask;
    }

    public Task<Usuario?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Usuarios.SingleOrDefaultAsync(usuario => usuario.Id == id, ct);

    public async Task<IReadOnlyList<Usuario>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Usuarios.OrderBy(usuario => usuario.Nombre).ThenBy(usuario => usuario.Id).ToListAsync(ct);

    public Task<Usuario?> ConsultarPorCorreoAsync(string correo, CancellationToken ct) =>
        contexto.Usuarios.SingleOrDefaultAsync(usuario => usuario.Correo == correo, ct);

    public Task<Usuario?> ConsultarPorTelefonoAsync(string telefonoWhatsApp, CancellationToken ct) =>
        contexto.Usuarios.SingleOrDefaultAsync(usuario => usuario.TelefonoWhatsApp == telefonoWhatsApp, ct);

    public Task<Usuario?> ConsultarPorIdentidadExternaAsync(string identidadExternaId, CancellationToken ct) =>
        contexto.Usuarios.AsNoTracking().SingleOrDefaultAsync(usuario => usuario.IdentidadExternaId == identidadExternaId, ct);

    public async Task<IReadOnlyList<Usuario>> ConsultarActivosPorRolAsync(Guid rolId, CancellationToken ct) =>
        await contexto.Usuarios.AsNoTracking().Where(usuario => usuario.RolId == rolId && usuario.Activo)
            .OrderBy(usuario => usuario.Nombre).ThenBy(usuario => usuario.Id).ToListAsync(ct);
}
