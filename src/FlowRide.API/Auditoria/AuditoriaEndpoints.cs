using FlowRide.API.Identidad;
using FlowRide.Application.Auditoria.Entrada.Puertos;

namespace FlowRide.API.Auditoria;

/// <summary>Adaptador de entrada HTTP de la auditoría legal (ADR-0011). Solo el Administrador del operador.</summary>
public static class AuditoriaEndpoints
{
    public static IEndpointRouteBuilder MapAuditoria(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auditoria/verificacion", async (IVerificarAuditoriaPuertoEntrada puerto, CancellationToken ct) =>
                TypedResults.Ok(await puerto.VerificarAsync(ct)))
            .WithTags("Auditoría")
            .RequireAuthorization(Politicas.Administrador);

        return app;
    }
}
