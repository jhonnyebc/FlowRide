using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Entrada.Puertos;

namespace FlowRide.API.Asignaciones;

/// <summary>
/// Adaptador de entrada HTTP de la asignación (RF-10 a RF-13, RF-21, RF-38). Consultar y forzar la asignación automática
/// es del Panel Web; intervenir a mano (RF-12) es del Operador logístico; configurar el motor, del Administrador.
/// </summary>
public static class AsignacionesEndpoints
{
    public static IEndpointRouteBuilder MapAsignaciones(this IEndpointRouteBuilder app)
    {
        var solicitudes = app.MapGroup("/solicitudes").WithTags("Asignación").RequireAuthorization(Politicas.PanelWeb);

        solicitudes.MapPost("/{id:guid}/asignar", async (Guid id, IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.AsignarAsync(id, ct)));

        solicitudes.MapGet("/{id:guid}/decisiones", async (Guid id, IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.DecisionesAsync(id, ct)));

        solicitudes.MapPost("/{id:guid}/asignacion-manual", async (Guid id, CandidatoManualPeticion peticion, ClaimsPrincipal usuario,
            IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
        {
            var asignacion = await puerto.AsignarManualAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/asignaciones?solicitudId={id}", asignacion);
        }).RequireAuthorization(Politicas.OperadorLogistico);

        solicitudes.MapPost("/{id:guid}/devolver-a-cola", async (Guid id, MotivoPeticion peticion, ClaimsPrincipal usuario,
            IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
        {
            await puerto.DevolverAColaAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.NoContent();
        }).RequireAuthorization(Politicas.OperadorLogistico);

        var asignaciones = app.MapGroup("/asignaciones").WithTags("Asignación").RequireAuthorization(Politicas.PanelWeb);

        asignaciones.MapGet("/", async ([AsParameters] BusquedaAsignacionesPeticion busqueda, IAsignacionesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct)));

        asignaciones.MapPost("/{id:guid}/reasignar", async (Guid id, CandidatoManualPeticion peticion, ClaimsPrincipal usuario,
                IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ReasignarAsync(id, peticion, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.OperadorLogistico);

        app.MapPost("/conductores/{conductorId:guid}/turnos/{turnoId:guid}/abandonar", async (Guid conductorId, Guid turnoId,
                MotivoPeticion peticion, ClaimsPrincipal usuario, IAsignacionesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(new { solicitudesDevueltas = await puerto.AbandonarTurnoAsync(conductorId, turnoId, peticion, usuario.UsuarioActual(), ct) }))
            .WithTags("Asignación")
            .RequireAuthorization(Politicas.OperadorLogistico);

        var motor = app.MapGroup("/motor/configuraciones").WithTags("Motor de asignación");

        motor.MapGet("/", async (IConfiguracionMotorPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarAsync(ct))).RequireAuthorization(Politicas.PanelWeb);

        motor.MapPost("/", async (ConfiguracionMotorPeticion peticion, ClaimsPrincipal usuario, IConfiguracionMotorPuertoEntrada puerto,
            CancellationToken ct) =>
        {
            var configuracion = await puerto.CrearAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/motor/configuraciones/{configuracion.Version}", configuracion);
        }).RequireAuthorization(Politicas.Administrador, Politicas.AutenticacionReciente);

        motor.MapPost("/{version:int}/activar", async (int version, ClaimsPrincipal usuario, IConfiguracionMotorPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActivarAsync(version, usuario.UsuarioActual(), ct))).RequireAuthorization(Politicas.Administrador, Politicas.AutenticacionReciente);

        return app;
    }
}
