using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Identidad.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Identidad.Repositorios;

namespace FlowRide.Application.Identidad.CasosUso;

/// <summary>
/// Activa o desactiva un usuario. La desactivación surte efecto en la siguiente petición, porque cada petición
/// comprueba el estado interno (ADM-SEG-01, ADR-0003); además se deshabilita su cuenta y se cierran sus sesiones.
/// </summary>
public sealed class CambiarEstadoUsuarioCasoUso(
    UsuarioDominio usuarioDominio,
    IUsuarioRepositorio usuarios,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    IProveedorIdentidad proveedor,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarEstadoUsuarioDominio, Usuario>
{
    public async Task<Usuario> EjecutarAsync(CambiarEstadoUsuarioDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var usuario = await usuarioDominio.ValidarUsuarioExistaAsync(datos.UsuarioId, ct);
        UsuarioDominio.ValidarNoEsElActor(usuario, datos.ActorId);

        // 1. Estado interno y auditoría: esto ya revoca el acceso. Si no cambia nada, no se audita.
        if (usuario.Activo != datos.Activo)
        {
            var antes = ValoresAuditables.De(usuario);
            if (datos.Activo) usuario.Activar();
            else usuario.Desactivar();

            await usuarios.ModificarAsync(usuario.Id, usuario, ct);
            await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
                datos.Activo ? AccionesAuditoria.UsuarioActivar : AccionesAuditoria.UsuarioDesactivar,
                AccionesAuditoria.RecursoUsuario, usuario.Id.ToString(), reloj.GetUtcNow(), ResultadoAuditoria.Exitoso,
                valoresAntes: antes, valoresDespues: ValoresAuditables.De(usuario)), ct);
            await unidadTrabajo.GuardarCambiosAsync(ct);
        }

        // 2. Cuenta del proveedor (solo usuarios del Panel Web). Siempre se sincroniza: repetir la acción repara un
        //    fallo anterior del proveedor.
        if (usuario.IdentidadExternaId is { } identidadExternaId)
        {
            if (datos.Activo) await proveedor.HabilitarAsync(identidadExternaId, ct);
            else await proveedor.DeshabilitarAsync(identidadExternaId, ct);
        }

        return usuario;
    }
}
