using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Puertos;
using Microsoft.AspNetCore.Mvc;

namespace FlowRide.API.Operacion;

/// <summary>
/// Adaptadores de entrada HTTP de los recursos operativos (RF-04, RF-05, RF-07, RF-26 a RF-28, RF-38, RF-41). Consultar
/// es del Panel Web (Administrador u Operador logístico); registrar y cambiar es del Administrador, salvo programar
/// turnos, que también hace el Operador logístico.
/// </summary>
public static class OperacionEndpoints
{
    public static IEndpointRouteBuilder MapOperacion(this IEndpointRouteBuilder app)
    {
        MapEmpresasCliente(app.MapGroup("/empresas-cliente").WithTags("Empresas cliente"));
        MapPasajeros(app.MapGroup("/pasajeros").WithTags("Pasajeros"));
        MapConductores(app.MapGroup("/conductores").WithTags("Conductores"));
        MapVehiculos(app.MapGroup("/vehiculos").WithTags("Vehículos"));
        return app;
    }

    private static void MapEmpresasCliente(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/", async ([AsParameters] BusquedaPeticion busqueda, IEmpresasClientePuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct))).RequireAuthorization(Politicas.PanelWeb);

        var administracion = grupo.MapGroup(string.Empty).RequireAuthorization(Politicas.Administrador);
        administracion.MapPost("/", async (EmpresaClientePeticion peticion, ClaimsPrincipal usuario,
            IEmpresasClientePuertoEntrada puerto, CancellationToken ct) =>
        {
            var empresa = await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/empresas-cliente/{empresa.Id}", empresa);
        });
        administracion.MapPut("/{id:guid}", async (Guid id, EmpresaClientePeticion peticion, ClaimsPrincipal usuario,
            IEmpresasClientePuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActualizarAsync(id, peticion, usuario.UsuarioActual(), ct)));
        administracion.MapPost("/{id:guid}/activar", async (Guid id, ClaimsPrincipal usuario,
            IEmpresasClientePuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, true, usuario.UsuarioActual(), ct)));
        administracion.MapPost("/{id:guid}/desactivar", async (Guid id, ClaimsPrincipal usuario,
            IEmpresasClientePuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, false, usuario.UsuarioActual(), ct)));
    }

    private static void MapPasajeros(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/", async ([AsParameters] BusquedaPeticion busqueda, IPasajerosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct))).RequireAuthorization(Politicas.PanelWeb);

        // El estado (activar/desactivar) es el del usuario: /api/usuarios/{id}/activar|desactivar.
        var administracion = grupo.MapGroup(string.Empty).RequireAuthorization(Politicas.Administrador);
        administracion.MapPost("/", async (PasajeroPeticion peticion, ClaimsPrincipal usuario,
            IPasajerosPuertoEntrada puerto, CancellationToken ct) =>
        {
            var pasajero = await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/pasajeros/{pasajero.Id}", pasajero);
        });
        administracion.MapPut("/{id:guid}", async (Guid id, PasajeroPeticion peticion, ClaimsPrincipal usuario,
            IPasajerosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActualizarAsync(id, peticion, usuario.UsuarioActual(), ct)));
    }

    private static void MapConductores(RouteGroupBuilder grupo)
    {
        var consulta = grupo.MapGroup(string.Empty).RequireAuthorization(Politicas.PanelWeb);
        consulta.MapGet("/", async ([AsParameters] BusquedaPeticion busqueda, IConductoresPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct)));
        consulta.MapGet("/disponibles", async (DateTime? inicio, DateTime? fin, string? zonaHoraria,
            IConductoresPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarDisponiblesAsync(inicio, fin, zonaHoraria, ct)));
        consulta.MapGet("/{id:guid}", async (Guid id, IConductoresPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConsultarAsync(id, ct)));
        consulta.MapPost("/{id:guid}/turnos", async (Guid id, TurnoPeticion peticion, ClaimsPrincipal usuario,
            IConductoresPuertoEntrada puerto, CancellationToken ct) =>
        {
            var turno = await puerto.ProgramarTurnoAsync(id, peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/conductores/{id}", turno);
        });
        consulta.MapGet("/{id:guid}/documentos/{documentoId:guid}/descarga", async (Guid id, Guid documentoId,
            IDocumentosConductorPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ObtenerUrlAsync(id, documentoId, ct)));

        var administracion = grupo.MapGroup(string.Empty).RequireAuthorization(Politicas.Administrador);
        administracion.MapPost("/", async (ConductorPeticion peticion, ClaimsPrincipal usuario,
            IConductoresPuertoEntrada puerto, CancellationToken ct) =>
        {
            var conductor = await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/conductores/{conductor.Id}", conductor);
        });
        administracion.MapPut("/{id:guid}", async (Guid id, ConductorPeticion peticion, ClaimsPrincipal usuario,
            IConductoresPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActualizarAsync(id, peticion, usuario.UsuarioActual(), ct)));
        administracion.MapPut("/{id:guid}/estado", async (Guid id, EstadoConductorPeticion peticion, ClaimsPrincipal usuario,
            IConductoresPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, peticion, usuario.UsuarioActual(), ct)));

        // Multipart con el archivo y sus datos. Sin cookies (solo bearer), así que no aplica antiforgery.
        administracion.MapPost("/{id:guid}/documentos", async (Guid id, IFormFile? archivo, [FromForm] string? tipo,
            [FromForm] DateOnly? fechaVencimiento, ClaimsPrincipal usuario, IDocumentosConductorPuertoEntrada puerto,
            CancellationToken ct) =>
        {
            await using var contenido = archivo?.OpenReadStream();
            var documento = await puerto.SubirAsync(id,
                new DocumentoPeticion { Tipo = tipo, FechaVencimiento = fechaVencimiento },
                archivo is null ? null : new ArchivoPeticion(archivo.FileName, archivo.ContentType, archivo.Length, contenido!),
                usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/conductores/{id}/documentos/{documento.Id}/descarga", documento);
        }).DisableAntiforgery();
        administracion.MapDelete("/{id:guid}/documentos/{documentoId:guid}", async (Guid id, Guid documentoId,
            ClaimsPrincipal usuario, IDocumentosConductorPuertoEntrada puerto, CancellationToken ct) =>
        {
            await puerto.EliminarAsync(id, documentoId, usuario.UsuarioActual(), ct);
            return TypedResults.NoContent();
        });
    }

    private static void MapVehiculos(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/", async ([AsParameters] BusquedaPeticion busqueda, IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.BuscarAsync(busqueda, ct))).RequireAuthorization(Politicas.PanelWeb);

        var administracion = grupo.MapGroup(string.Empty).RequireAuthorization(Politicas.Administrador);
        administracion.MapPost("/", async (VehiculoPeticion peticion, ClaimsPrincipal usuario,
            IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
        {
            var vehiculo = await puerto.RegistrarAsync(peticion, usuario.UsuarioActual(), ct);
            return TypedResults.Created($"/api/vehiculos/{vehiculo.Id}", vehiculo);
        });
        administracion.MapPut("/{id:guid}", async (Guid id, VehiculoPeticion peticion, ClaimsPrincipal usuario,
            IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ActualizarAsync(id, peticion, usuario.UsuarioActual(), ct)));
        administracion.MapPost("/{id:guid}/activar", async (Guid id, ClaimsPrincipal usuario,
            IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, true, usuario.UsuarioActual(), ct)));
        administracion.MapPost("/{id:guid}/desactivar", async (Guid id, ClaimsPrincipal usuario,
            IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.CambiarEstadoAsync(id, false, usuario.UsuarioActual(), ct)));
        administracion.MapPut("/{id:guid}/conductor-habitual", async (Guid id, ConductorHabitualPeticion peticion,
            ClaimsPrincipal usuario, IVehiculosPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.AsignarConductorHabitualAsync(id, peticion, usuario.UsuarioActual(), ct)));
    }
}
