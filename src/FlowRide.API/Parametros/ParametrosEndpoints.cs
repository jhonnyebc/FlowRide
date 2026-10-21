using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Parametros.Entrada.Dto;
using FlowRide.Application.Parametros.Entrada.Puertos;

namespace FlowRide.API.Parametros;

/// <summary>Parámetros del operador (ADR-0016): los consulta el Panel Web y solo el Administrador los cambia.</summary>
public static class ParametrosEndpoints
{
    public static IEndpointRouteBuilder MapParametros(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/parametros").WithTags("Parámetros");

        grupo.MapGet("/", async (IParametrosPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.ConsultarAsync(ct))).RequireAuthorization(Politicas.PanelWeb);

        grupo.MapPut("/{clave}", async (string clave, CambiarParametroPeticion peticion, ClaimsPrincipal usuario,
                IParametrosPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.CambiarAsync(clave, peticion, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.Administrador, Politicas.AutenticacionReciente);

        return app;
    }
}
