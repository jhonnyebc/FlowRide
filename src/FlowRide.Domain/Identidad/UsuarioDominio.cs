using System.Net.Mail;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Identidad;

/// <summary>
/// Reglas de negocio sobre usuarios que necesitan consultar el Silo. Las que dependen solo del usuario viven en la
/// entidad <see cref="Usuario"/>.
/// </summary>
public sealed class UsuarioDominio(IUsuarioRepositorio usuarios)
{
    public async Task<Usuario> ValidarUsuarioExistaAsync(Guid usuarioId, CancellationToken ct) =>
        await usuarios.ConsultarPorIdAsync(usuarioId, ct)
        ?? throw new FlowRideExcepcion($"El usuario {usuarioId} no existe.");

    /// <summary>El correo identifica a un único usuario del operador (también es su usuario en el Panel Web).</summary>
    public async Task ValidarCorreoDisponibleAsync(string correo, Guid? usuarioId, CancellationToken ct)
    {
        if (await usuarios.ConsultarPorCorreoAsync(correo.Trim(), ct) is { } titular && titular.Id != usuarioId)
            throw new FlowRideExcepcion($"Ya existe un usuario con el correo {correo.Trim()}.");
    }

    /// <summary>El número de WhatsApp identifica al usuario en el canal (ADR-0008), así que no se repite.</summary>
    public async Task ValidarTelefonoDisponibleAsync(string telefonoWhatsApp, Guid? usuarioId, CancellationToken ct)
    {
        if (await usuarios.ConsultarPorTelefonoAsync(telefonoWhatsApp.Trim(), ct) is { } titular && titular.Id != usuarioId)
            throw new FlowRideExcepcion($"Ya existe un usuario con el número de WhatsApp {telefonoWhatsApp.Trim()}.");
    }

    /// <summary>El correo es el usuario de inicio de sesión y el destino del correo para definir la contraseña.</summary>
    public static void ValidarFormatoCorreo(string correo)
    {
        ArgumentNullException.ThrowIfNull(correo);
        var valor = correo.Trim();
        if (!MailAddress.TryCreate(valor, out var direccion) || direccion.Address != valor)
            throw new FlowRideExcepcion($"El correo '{valor}' no es válido.");
    }

    /// <summary>Desde el Panel Web solo se dan de alta Administradores y Operadores logísticos.</summary>
    public static Rol ValidarRolDelPanel(string? nombreRol)
    {
        var rol = RolesSistema.PorNombre(nombreRol);
        if (rol is null || !RolesSistema.EsDelPanel(rol.Id))
            throw new FlowRideExcepcion(
                $"El rol '{nombreRol}' no es válido: usa {string.Join(" o ", RolesSistema.DelPanel)}.");
        return rol;
    }

    /// <summary>Pasajeros y conductores no usan el Panel Web: su rol no se cambia por uno del panel ni al revés.</summary>
    public static void ValidarUsuarioDelPanel(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (!RolesSistema.EsDelPanel(usuario.RolId))
            throw new FlowRideExcepcion($"El usuario {usuario.Id} no es un usuario del Panel Web.");
    }

    /// <summary>Nadie se desactiva ni se cambia el rol a sí mismo: evita que el operador se quede sin administrador.</summary>
    public static void ValidarNoEsElActor(Usuario usuario, Guid? actorId)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (actorId == usuario.Id)
            throw new FlowRideExcepcion("No puedes cambiar tu propio estado ni tu propio rol; pídeselo a otro administrador.");
    }
}
