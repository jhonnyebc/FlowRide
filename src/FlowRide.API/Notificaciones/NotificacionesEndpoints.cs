using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using FlowRide.API.Identidad;
using FlowRide.Application.Notificaciones.Entrada.Dto;
using FlowRide.Application.Notificaciones.Entrada.Puertos;
using FlowRide.Application.Notificaciones.Salida.Puertos;

namespace FlowRide.API.Notificaciones;

/// <summary>
/// Adaptador de entrada HTTP de las notificaciones (RF-45, ADR-0015, ADR-0016): alertas del Panel Web (lista, marcar leída
/// y envío en vivo por Server-Sent Events) y registro de plantillas de WhatsApp (solo el Administrador las cambia).
/// </summary>
public static class NotificacionesEndpoints
{
    /// <summary>Cada cuánto el envío en vivo busca alertas nuevas (lee de la base: sirve con varias réplicas).</summary>
    public static readonly TimeSpan IntervaloEnVivo = TimeSpan.FromSeconds(2);

    public static IEndpointRouteBuilder MapNotificaciones(this IEndpointRouteBuilder app)
    {
        var alertas = app.MapGroup("/alertas").WithTags("Alertas").RequireAuthorization(Politicas.PanelWeb);

        alertas.MapGet("/", async ([AsParameters] BusquedaAlertasPeticion busqueda, INotificacionesPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.BuscarAlertasAsync(busqueda, ct)));

        alertas.MapPost("/{id:guid}/leida", async (Guid id, ClaimsPrincipal usuario, INotificacionesPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.MarcarAlertaLeidaAsync(id, usuario.UsuarioActual(), ct)));

        // En vivo: manda las alertas posteriores a "desde" (o al Last-Event-ID al reconectar) y luego las nuevas.
        alertas.MapGet("/en-vivo", (long? desde, HttpRequest peticion, INotificacionesPuertoEntrada puerto, CancellationToken ct) =>
        {
            var ultima = desde ?? (long.TryParse(peticion.Headers["Last-Event-ID"], NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0);
            return TypedResults.ServerSentEvents(EnVivoAsync(puerto, ultima, ct));
        });

        var plantillas = app.MapGroup("/notificaciones/plantillas").WithTags("Notificaciones");

        plantillas.MapGet("/", async (INotificacionesPuertoEntrada puerto, CancellationToken ct) =>
            Results.Ok(await puerto.PlantillasAsync(ct))).RequireAuthorization(Politicas.PanelWeb);

        plantillas.MapPut("/{clave}", async (string clave, PlantillaPeticion peticion, ClaimsPrincipal usuario, INotificacionesPuertoEntrada puerto,
                CancellationToken ct) =>
            Results.Ok(await puerto.ActualizarPlantillaAsync(clave, peticion, usuario.UsuarioActual(), ct)))
            .RequireAuthorization(Politicas.Administrador, Politicas.AutenticacionReciente);

        return app;
    }

    private static async IAsyncEnumerable<SseItem<AlertaLectura>> EnVivoAsync(INotificacionesPuertoEntrada puerto, long desde,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var ultima = desde;
        while (!ct.IsCancellationRequested)
        {
            foreach (var alerta in await puerto.AlertasPosterioresAsync(ultima, ct))
            {
                ultima = alerta.Secuencia;
                yield return new SseItem<AlertaLectura>(alerta, "alerta") { EventId = alerta.Secuencia.ToString(CultureInfo.InvariantCulture) };
            }

            try
            {
                await Task.Delay(IntervaloEnVivo, ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }
}
