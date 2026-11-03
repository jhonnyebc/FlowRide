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
/// Cambia el rol de un usuario del Panel Web (RF-02). El rol vive en FlowRide, así que el cambio aplica en la
/// siguiente petición sin esperar a que expire el token.
/// </summary>
public sealed class CambiarRolUsuarioCasoUso(
    UsuarioDominio usuarioDominio,
    IUsuarioRepositorio usuarios,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    IProveedorIdentidad proveedor,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarRolUsuarioDominio, Usuario>
{
    public async Task<Usuario> EjecutarAsync(CambiarRolUsuarioDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var rol = UsuarioDominio.ValidarRolDelPanel(datos.Rol);
        var usuario = await usuarioDominio.ValidarUsuarioExistaAsync(datos.UsuarioId, ct);
        UsuarioDominio.ValidarNoEsElActor(usuario, datos.ActorId);
        UsuarioDominio.ValidarUsuarioDelPanel(usuario);

        if (usuario.RolId == rol.Id)
            return usuario;

        var antes = ValoresAuditables.De(usuario);
        usuario.CambiarRol(rol.Id);
        await usuarios.ModificarAsync(usuario.Id, usuario, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId, AccionesAuditoria.UsuarioCambiarRol,
            AccionesAuditoria.RecursoUsuario, usuario.Id.ToString(), reloj.GetUtcNow(), ResultadoAuditoria.Exitoso,
            valoresAntes: antes, valoresDespues: ValoresAuditables.De(usuario)), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);

        // Quien pasa a un rol privilegiado configura su TOTP en el próximo inicio de sesión.
        if (RolesSistema.ExigeDobleFactor(rol.Id) && usuario.IdentidadExternaId is { } externa)
            await proveedor.ExigirDobleFactorAsync(externa, ct);
        return usuario;
    }
}
