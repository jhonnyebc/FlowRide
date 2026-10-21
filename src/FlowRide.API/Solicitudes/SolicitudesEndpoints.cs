using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Puertos;

namespace FlowRide.API.Solicitudes;

/// <summary>
/// Adaptador de entrada HTTP de las solicitudes e itinerarios (RF-08, RF-09, RF-29 a RF-32) para el Panel Web
/// (Administrador u Operador logístico). El rol del usuario decide la visibilidad de las solicitudes personales y si
/// puede cancelar fuera de la ventana.
/// </summary>
public static class SolicitudesEndpoints
{
    public static IEndpointRouteBuilder MapSolicitudes(this IEndpointRouteBuilder app)
    {
        var solicitudes = app.MapGroup("/solicitudes").WithTags("Solicitudes").RequireAuthorization(Politicas.PanelWeb);

        solicitudes.MapGet("/", async ([AsParameters] BusquedaSolicitudesPeticion busqueda, ClaimsPrincipal usuario,
                ISolicitudesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, Rol(usuario), ct)));

        solicitudes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal usuario, ISolicitudesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarAsync(id, Rol(usuario), ct)));

        solicitudes.MapPost("/", async (SolicitudPeticion peticion, ClaimsPrincipal usuario, ISolicitudesPuertoEntrada puerto,
            CancellationToken ct) =>
        {
            var detalle = await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), Rol(usuario), ct);
            return TypedResults.Created($"/api/solicitudes/{detalle.Solicitud.Id}", detalle);
        });

        solicitudes.MapPut("/{id:guid}", async (Guid id, SolicitudPeticion peticion, ClaimsPrincipal usuario,
                ISolicitudesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ModificarAsync(id, peticion, usuario.UsuarioActual(), Rol(usuario), ct)));

        solicitudes.MapPost("/{id:guid}/cancelar", async (Guid id, CancelarSolicitudPeticion peticion, ClaimsPrincipal usuario,
                ISolicitudesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CancelarAsync(id, peticion, usuario.UsuarioActual(), Rol(usuario), ct)));

        var itinerarios = app.MapGroup("/itinerarios").WithTags("Itinerarios").RequireAuthorization(Politicas.PanelWeb);

        itinerarios.MapGet("/", async ([AsParameters] BusquedaItinerariosPeticion busqueda, IItinerariosPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct)));

        itinerarios.MapPost("/", async (ItinerariosPeticion peticion, ClaimsPrincipal usuario, IItinerariosPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), ct)));

        return app;
    }

    private static string Rol(ClaimsPrincipal usuario) => usuario.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
