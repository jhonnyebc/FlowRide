using FlowRide.Application.Notificaciones.Entrada.Dto;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;

namespace FlowRide.Application.Notificaciones.Entrada.Puertos;

/// <summary>Puerto de entrada de notificaciones: el worker de la Outbox, el envío de correos y el Panel Web.</summary>
public interface INotificacionesPuertoEntrada
{
    /// <summary>Convierte un evento de la Outbox en notificaciones; false si el tipo no notifica nada.</summary>
    Task<bool> ProcesarEventoAsync(Guid eventId, string tipoEvento, string contenido, DateTimeOffset ocurridoEn, CancellationToken ct);

    Task<IReadOnlyList<Guid>> CorreosPendientesAsync(int maximo, CancellationToken ct);

    /// <summary>Envía un correo de la bandeja; true si quedó enviado.</summary>
    Task<bool> EnviarCorreoAsync(Guid correoId, CancellationToken ct);

    Task<PaginaRespuesta<AlertaLectura>> BuscarAlertasAsync(BusquedaAlertasPeticion peticion, CancellationToken ct);

    /// <summary>Alertas nuevas desde la secuencia dada (envío en vivo por SSE).</summary>
    Task<IReadOnlyList<AlertaLectura>> AlertasPosterioresAsync(long despuesDe, CancellationToken ct);

    Task<AlertaLeidaRespuesta> MarcarAlertaLeidaAsync(Guid alertaId, Guid? actorId, CancellationToken ct);

    Task<IReadOnlyList<PlantillaRespuesta>> PlantillasAsync(CancellationToken ct);

    Task<PlantillaRespuesta> ActualizarPlantillaAsync(string clave, PlantillaPeticion peticion, Guid? actorId, CancellationToken ct);
}
