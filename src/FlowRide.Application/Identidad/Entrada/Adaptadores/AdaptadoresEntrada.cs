using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Identidad.Entrada.Puertos;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Identidad.Entrada.Adaptadores;

public sealed class CrearUsuarioAdaptadorEntrada(ICasoUsoConRetorno<CrearUsuarioDominio, Usuario> casoUso)
    : ICrearUsuarioPuertoEntrada
{
    public async Task<UsuarioRespuesta> CrearAsync(CrearUsuarioPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        var datos = PeticionesUsuario.Convertir(peticion, exigirRol: true, actorId);
        return MapeoUsuarios.ARespuesta(await casoUso.EjecutarAsync(datos, ct));
    }
}

public sealed class AsegurarAdministradorInicialAdaptadorEntrada(
    ICasoUsoConRetorno<CrearUsuarioDominio, ResultadoAdministradorInicial> casoUso)
    : IAsegurarAdministradorInicialPuertoEntrada
{
    public async Task<AdministradorInicialRespuesta> AsegurarAsync(CrearUsuarioPeticion peticion, CancellationToken ct)
    {
        var datos = PeticionesUsuario.Convertir(peticion, exigirRol: false, actorId: null);
        var resultado = await casoUso.EjecutarAsync(datos, ct);
        return new AdministradorInicialRespuesta(
            MapeoUsuarios.ARespuesta(resultado.Usuario),
            resultado.Nuevo,
            resultado.Nuevo
                ? $"Administrador creado; se envió a {resultado.Usuario.Correo} el correo para definir su contraseña."
                : "El administrador ya existía.");
    }
}

public sealed class CambiarEstadoUsuarioAdaptadorEntrada(
    ICasoUsoConRetorno<CambiarEstadoUsuarioDominio, Usuario> casoUso) : ICambiarEstadoUsuarioPuertoEntrada
{
    public async Task<UsuarioRespuesta> CambiarEstadoAsync(Guid usuarioId, bool activo, Guid? actorId, CancellationToken ct) =>
        MapeoUsuarios.ARespuesta(await casoUso.EjecutarAsync(new CambiarEstadoUsuarioDominio(usuarioId, activo, actorId), ct));
}

public sealed class CambiarRolUsuarioAdaptadorEntrada(
    ICasoUsoConRetorno<CambiarRolUsuarioDominio, Usuario> casoUso) : ICambiarRolUsuarioPuertoEntrada
{
    public async Task<UsuarioRespuesta> CambiarRolAsync(
        Guid usuarioId, CambiarRolUsuarioPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Rol))
            throw new FlowRideExcepcion("rol es obligatorio.");
        return MapeoUsuarios.ARespuesta(
            await casoUso.EjecutarAsync(new CambiarRolUsuarioDominio(usuarioId, peticion.Rol.Trim(), actorId), ct));
    }
}

public sealed class ActualizarUsuarioAdaptadorEntrada(
    ICasoUsoConRetorno<ActualizarUsuarioDominio, Usuario> casoUso) : IActualizarUsuarioPuertoEntrada
{
    public async Task<UsuarioRespuesta> ActualizarAsync(
        Guid usuarioId, ActualizarUsuarioPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (peticion is null
            || string.IsNullOrWhiteSpace(peticion.Nombre)
            || string.IsNullOrWhiteSpace(peticion.Correo)
            || string.IsNullOrWhiteSpace(peticion.TelefonoWhatsApp))
        {
            throw new FlowRideExcepcion("nombre, correo y telefonoWhatsApp son obligatorios.");
        }

        var datos = new ActualizarUsuarioDominio(usuarioId, peticion.Nombre.Trim(), peticion.Correo.Trim(),
            TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp), actorId);
        return MapeoUsuarios.ARespuesta(await casoUso.EjecutarAsync(datos, ct));
    }
}

public sealed class ConsultarUsuariosAdaptadorEntrada(
    ICasoUsoConRetorno<ConsultarUsuariosDominio, IReadOnlyList<Usuario>> casoUso) : IConsultarUsuariosPuertoEntrada
{
    public async Task<IReadOnlyList<UsuarioRespuesta>> ConsultarAsync(string? rol, CancellationToken ct)
    {
        var usuarios = await casoUso.EjecutarAsync(new ConsultarUsuariosDominio(rol?.Trim()), ct);
        return usuarios.OrderBy(usuario => usuario.Nombre, StringComparer.Ordinal)
            .Select(MapeoUsuarios.ARespuesta).ToList();
    }
}

public sealed class ResolverIdentidadAdaptadorEntrada(
    ICasoUsoConRetorno<ResolverIdentidadDominio, Usuario?> casoUso) : IResolverIdentidadPuertoEntrada
{
    public async Task<UsuarioAutenticadoRespuesta?> ResolverAsync(string identidadExternaId, CancellationToken ct)
    {
        var usuario = await casoUso.EjecutarAsync(new ResolverIdentidadDominio(identidadExternaId), ct);
        return usuario is null
            ? null
            : new UsuarioAutenticadoRespuesta(usuario.Id, usuario.Nombre, usuario.Correo, RolesSistema.NombrePorId(usuario.RolId));
    }
}

internal static class PeticionesUsuario
{
    public static CrearUsuarioDominio Convertir(CrearUsuarioPeticion? peticion, bool exigirRol, Guid? actorId)
    {
        if (peticion is null
            || string.IsNullOrWhiteSpace(peticion.Nombre)
            || string.IsNullOrWhiteSpace(peticion.Correo)
            || string.IsNullOrWhiteSpace(peticion.TelefonoWhatsApp)
            || (exigirRol && string.IsNullOrWhiteSpace(peticion.Rol)))
        {
            throw new FlowRideExcepcion(exigirRol
                ? "nombre, correo, telefonoWhatsApp y rol son obligatorios."
                : "nombre, correo y telefonoWhatsApp son obligatorios.");
        }

        return new CrearUsuarioDominio(
            peticion.Nombre.Trim(),
            peticion.Correo.Trim(),
            TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp),
            exigirRol ? peticion.Rol!.Trim() : RolesSistema.Administrador,
            actorId);
    }
}

internal static class MapeoUsuarios
{
    public static UsuarioRespuesta ARespuesta(Usuario usuario) => new(
        usuario.Id,
        usuario.Nombre,
        usuario.Correo,
        usuario.TelefonoWhatsApp,
        RolesSistema.NombrePorId(usuario.RolId),
        usuario.Activo,
        usuario.CreadoEn);
}
