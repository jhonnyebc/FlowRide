using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Puertos;

namespace FlowRide.API.Viajes;

/// <summary>
/// Adaptador de entrada HTTP de los viajes (RF-31, RF-34, RF-37, RF-40, RF-54). Consultar es del Panel Web; las acciones
/// del conductor y del pasajero las ejecuta el Operador logístico en su nombre hasta que llegue el canal WhatsApp (Fase 7).
/// El rol del usuario decide la visibilidad de las solicitudes personales.
/// </summary>
public static class ViajesEndpoints
{
    public static IEndpointRouteBuilder MapViajes(this IEndpointRouteBuilder app)
    {
        var asignaciones = app.MapGroup("/asignaciones").WithTags("Viajes").RequireAuthorization(Politicas.OperadorLogistico);

        asignaciones.MapPost("/{id:guid}/aceptar", async (Guid id, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto, CancellationToken ct) =>
        {
            var viaje = await puerto.AceptarAsignacionAsync(id, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/viajes/{viaje.Id}", viaje);
        });

        asignaciones.MapPost("/{id:guid}/rechazar", async (Guid id, MotivoPeticion peticion, ClaimsPrincipal usuario,
            IViajesPuertoEntrada puerto, CancellationToken ct) =>
        {
            await puerto.RechazarAsignacionAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.NoContent();
        });

        var viajes = app.MapGroup("/viajes").WithTags("Viajes").RequireAuthorization(Politicas.PanelWeb);

        viajes.MapGet("/", async ([AsParameters] BusquedaViajesPeticion busqueda, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, Rol(usuario), ct)));

        viajes.MapGet("/monitoreo", async (ClaimsPrincipal usuario, IViajesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.MonitorearAsync(Rol(usuario), ct)));

        viajes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarAsync(id, Rol(usuario), ct)));

        viajes.MapPost("/{id:guid}/iniciar", async (Guid id, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto, CancellationToken ct) =>
                TypedResults.Ok(await puerto.IniciarAsync(id, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.OperadorLogistico);

        viajes.MapPost("/{id:guid}/finalizar", async (Guid id, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto, CancellationToken ct) =>
                TypedResults.Ok(await puerto.FinalizarAsync(id, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.OperadorLogistico);

        viajes.MapPost("/{id:guid}/paradas", async (Guid id, ParadaPeticion peticion, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto,
            CancellationToken ct) =>
        {
            var parada = await puerto.RegistrarParadaAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/viajes/{id}", parada);
        }).RequireAuthorization(Politicas.OperadorLogistico);

        viajes.MapPost("/{id:guid}/paradas/{paradaId:guid}/llegada", async (Guid id, Guid paradaId, ClaimsPrincipal usuario,
                IViajesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.RegistrarLlegadaAsync(id, paradaId, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.OperadorLogistico);

        viajes.MapPost("/{id:guid}/incidentes", async (Guid id, IncidentePeticion peticion, ClaimsPrincipal usuario,
            IViajesPuertoEntrada puerto, CancellationToken ct) =>
        {
            var incidente = await puerto.ReportarIncidenteAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/viajes/{id}", incidente);
        }).RequireAuthorization(Politicas.OperadorLogistico);

        viajes.MapPost("/{id:guid}/calificacion", async (Guid id, CalificacionPeticion peticion, ClaimsPrincipal usuario,
            IViajesPuertoEntrada puerto, CancellationToken ct) =>
        {
            var calificacion = await puerto.CalificarAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/viajes/{id}", calificacion);
        }).RequireAuthorization(Politicas.OperadorLogistico);

        app.MapGet("/solicitudes/{id:guid}/trazabilidad", async (Guid id, ClaimsPrincipal usuario, IViajesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.TrazabilidadAsync(id, Rol(usuario), ct)))
            .WithTags("Viajes")
            .RequireAuthorization(Politicas.PanelWeb);

        return app;
    }

    private static string Rol(ClaimsPrincipal usuario) => usuario.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
