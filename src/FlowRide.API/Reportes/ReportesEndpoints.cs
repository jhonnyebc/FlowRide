using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Reportes.Entrada.Dto;
using FlowRide.Application.Reportes.Entrada.Puertos;

namespace FlowRide.API.Reportes;

/// <summary>
/// Adaptador de entrada HTTP de los reportes y la vista operacional (RF-36, RF-42, RF-46 a RF-49). Los consultan y los
/// exportan el Administrador y el Operador logístico (decisión del 2026-10-19); el rol decide la visibilidad de las
/// direcciones de las solicitudes personales.
/// </summary>
public static class ReportesEndpoints
{
    public static IEndpointRouteBuilder MapReportes(this IEndpointRouteBuilder app)
    {
        var reportes = app.MapGroup("/reportes").WithTags("Reportes").RequireAuthorization(Politicas.PanelWeb);

        reportes.MapGet("/vista-centralizada", async ([AsParameters] VistaCentralizadaPeticion peticion, ClaimsPrincipal usuario,
                IReportesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.VistaCentralizadaAsync(peticion, Rol(usuario), ct)));

        reportes.MapGet("/indicadores", async ([AsParameters] ReportePeticion peticion, IReportesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.IndicadoresAsync(peticion, ct)));

        reportes.MapGet("/viajes", async ([AsParameters] ReportePeticion peticion, ClaimsPrincipal usuario, IReportesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.ViajesAsync(peticion, Rol(usuario), ct)));

        reportes.MapGet("/solicitudes", async ([AsParameters] ReportePeticion peticion, ClaimsPrincipal usuario, IReportesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.SolicitudesAsync(peticion, Rol(usuario), ct)));

        reportes.MapGet("/conductores", async ([AsParameters] ReportePeticion peticion, IReportesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.ConductoresAsync(peticion, ct)));

        reportes.MapGet("/empresas", async ([AsParameters] ReportePeticion peticion, IReportesPuertoEntrada puerto, CancellationToken ct) =>
            TypedResults.Ok(await puerto.EmpresasAsync(peticion, ct)));

        reportes.MapPost("/{tipo}/exportar", async (string tipo, ReportePeticion peticion, ClaimsPrincipal usuario, IReportesPuertoEntrada puerto,
                CancellationToken ct) =>
            TypedResults.Ok(await puerto.ExportarAsync(tipo, peticion, Rol(usuario), usuario.UsuarioActual(), ct)));

        return app;
    }

    private static string Rol(ClaimsPrincipal usuario) => usuario.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
