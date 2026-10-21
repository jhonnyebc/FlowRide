using System.Security.Claims;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Identidad.Entrada.Puertos;

namespace FlowRide.API.Identidad;

/// <summary>
/// Adaptador de entrada HTTP de la gestión de usuarios del Panel Web (RF-01, RF-02). Solo conoce los puertos de
/// entrada y sus DTO; el actor de la auditoría es el usuario autenticado.
/// </summary>
public static class UsuariosEndpoints
{
    public static IEndpointRouteBuilder MapUsuarios(this IEndpointRouteBuilder app)
    {
        var usuarios = app.MapGroup("/usuarios").WithTags("Usuarios");

        // Cualquier usuario del panel: quién soy y con qué rol.
        usuarios.MapGet("/yo", (ClaimsPrincipal usuario) => TypedResults.Ok(new UsuarioAutenticadoRespuesta(
                usuario.UsuarioActual()!.Value,
                usuario.FindFirstValue(ClaimTypes.Name)!,
                usuario.FindFirstValue(ClaimTypes.Email),
                usuario.FindFirstValue(ClaimTypes.Role)!)))
            .RequireAuthorization(Politicas.PanelWeb);

        var administracion = usuarios.MapGroup(string.Empty).RequireAuthorization(Politicas.Administrador);

        administracion.MapGet("/", async (string? rol, IConsultarUsuariosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarAsync(rol, ct)));

        administracion.MapPost("/", async (CrearUsuarioPeticion peticion, ClaimsPrincipal usuario,
            ICrearUsuarioPuertoEntrada puerto, CancellationToken ct) =>
        {
            var creado = await puerto.CrearAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/usuarios/{creado.Id}", creado);
        }).RequireAuthorization(Politicas.AutenticacionReciente);

        administracion.MapPost("/{id:guid}/desactivar", async (Guid id, ClaimsPrincipal usuario,
                ICambiarEstadoUsuarioPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, activo: false, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.AutenticacionReciente);

        administracion.MapPost("/{id:guid}/activar", async (Guid id, ClaimsPrincipal usuario,
                ICambiarEstadoUsuarioPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, activo: true, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.AutenticacionReciente);

        administracion.MapPut("/{id:guid}/rol", async (Guid id, CambiarRolUsuarioPeticion peticion, ClaimsPrincipal usuario,
                ICambiarRolUsuarioPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarRolAsync(id, peticion, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.AutenticacionReciente);

        // RF-24: el correo es el usuario de inicio de sesión, así que también pide un inicio de sesión reciente.
        administracion.MapPut("/{id:guid}", async (Guid id, ActualizarUsuarioPeticion peticion, ClaimsPrincipal usuario,
                IActualizarUsuarioPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActualizarAsync(id, peticion, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.AutenticacionReciente);

        return app;
    }
}
