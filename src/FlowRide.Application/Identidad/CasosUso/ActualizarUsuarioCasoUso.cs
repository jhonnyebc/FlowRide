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
/// Actualiza el nombre, el correo y el WhatsApp de un usuario del Panel Web (RF-24). El correo es su usuario de inicio
/// de sesión: si cambia, la cuenta del proveedor lo toma y le pide confirmar la nueva dirección.
/// </summary>
public sealed class ActualizarUsuarioCasoUso(
    UsuarioDominio usuarioDominio,
    IUsuarioRepositorio usuarios,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    IProveedorIdentidad proveedor,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarUsuarioDominio, Usuario>
{
    public async Task<Usuario> EjecutarAsync(ActualizarUsuarioDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // 1. Reglas, antes de tocar el proveedor.
        var usuario = await usuarioDominio.ValidarUsuarioExistaAsync(datos.UsuarioId, ct);
        UsuarioDominio.ValidarUsuarioDelPanel(usuario);
        UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
        await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, usuario.Id, ct);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, usuario.Id, ct);

        var campos = CamposCambiados(usuario, datos);
        if (campos.Count == 0)
            return usuario;

        // 2. Proveedor primero: si después falla la base, el reintento vuelve a aplicar lo mismo sin daño.
        if (usuario.IdentidadExternaId is { } externa && (campos.Contains("nombre") || campos.Contains("correo")))
            await proveedor.ActualizarCuentaAsync(externa, datos.Correo, datos.Nombre, ct);

        // 3. Usuario interno y su auditoría, en una sola transacción. La auditoría dice qué campos cambiaron, no sus
        // valores: son datos personales (ADR-0013).
        usuario.ActualizarDatos(datos.Nombre, datos.TelefonoWhatsApp, datos.Correo);
        await usuarios.ModificarAsync(usuario.Id, usuario, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId, AccionesAuditoria.UsuarioActualizar,
            AccionesAuditoria.RecursoUsuario, usuario.Id.ToString(), reloj.GetUtcNow(), ResultadoAuditoria.Exitoso,
            valoresDespues: new Dictionary<string, string?>(StringComparer.Ordinal) { ["campos"] = string.Join(",", campos) }), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return usuario;
    }

    private static List<string> CamposCambiados(Usuario usuario, ActualizarUsuarioDominio datos)
    {
        var campos = new List<string>();
        if (!string.Equals(usuario.Nombre, datos.Nombre, StringComparison.Ordinal)) campos.Add("nombre");
        if (!string.Equals(usuario.Correo, datos.Correo, StringComparison.OrdinalIgnoreCase)) campos.Add("correo");
        if (!string.Equals(usuario.TelefonoWhatsApp, datos.TelefonoWhatsApp, StringComparison.Ordinal)) campos.Add("telefonoWhatsApp");
        return campos;
    }
}
