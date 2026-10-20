using FlowRide.Application.Borde.Entrada.Dto;

namespace FlowRide.Application.Borde.Entrada.Puertos;

/// <summary>Lo que el borde común (ADR-0012) necesita de Application: la tabla de ruteo y el router de webhooks.</summary>
public interface IBordePuertoEntrada
{
    /// <summary>Lee las rutas de los operadores activos; los hosts quedan como <c>{subdominio}.{dominioBase}</c>.</summary>
    Task<RutasVigentes> CargarRutasAsync(string dominioBase, CancellationToken ct);

    bool VerificarSuscripcion(string? modo, string? token);

    Task<WebhookEnrutadoRespuesta> EnrutarWebhookAsync(string cuerpo, string? firma, RutasVigentes rutas, CancellationToken ct);
}
