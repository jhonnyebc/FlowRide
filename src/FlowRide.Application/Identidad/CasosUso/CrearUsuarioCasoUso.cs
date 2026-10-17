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
/// Alta de un usuario del Panel Web (RF-01, RF-02): cuenta en el realm del operador, usuario interno con su rol y
/// evento de auditoría. Al final, Keycloak le envía el correo para definir su contraseña.
/// </summary>
public sealed class CrearUsuarioCasoUso(
    UsuarioDominio usuarioDominio,
    IUsuarioRepositorio usuarios,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    IProveedorIdentidad proveedor,
    TimeProvider reloj) : ICasoUsoConRetorno<CrearUsuarioDominio, Usuario>
{
    public async Task<Usuario> EjecutarAsync(CrearUsuarioDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // 1. Reglas, antes de tocar el proveedor.
        var rol = UsuarioDominio.ValidarRolDelPanel(datos.Rol);
        UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
        await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, ct);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, ct);

        // 2. Cuenta en el proveedor primero: si después falla la base, al reintentar se reutiliza (se busca por correo).
        var identidadExternaId = await proveedor.AsegurarUsuarioAsync(datos.Correo.Trim(), datos.Nombre.Trim(), ct);

        // 3. Usuario interno y su auditoría, en una sola transacción.
        var ahora = reloj.GetUtcNow();
        var usuario = new Usuario(Guid.CreateVersion7(ahora), datos.Nombre, datos.TelefonoWhatsApp, rol.Id, ahora,
            datos.Correo, identidadExternaId);
        await usuarios.CrearAsync(usuario, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId, AccionesAuditoria.UsuarioCrear,
            AccionesAuditoria.RecursoUsuario, usuario.Id.ToString(), ahora, ResultadoAuditoria.Exitoso,
            valoresDespues: ValoresAuditables.De(usuario)), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);

        // 4. Correo para definir la contraseña (nunca la conoce FlowRide).
        await proveedor.EnviarConfiguracionContrasenaAsync(identidadExternaId, ct);
        return usuario;
    }
}

/// <summary>Lo que la auditoría guarda de un usuario: su rol y su estado, sin datos personales (ADR-0013).</summary>
internal static class ValoresAuditables
{
    public static Dictionary<string, string?> De(Usuario usuario) => new(StringComparer.Ordinal)
    {
        ["rol"] = RolesSistema.NombrePorId(usuario.RolId),
        ["activo"] = usuario.Activo ? "true" : "false"
    };
}
