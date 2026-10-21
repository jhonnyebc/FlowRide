using FlowRide.Domain.Entidades;

namespace FlowRide.Application.Horarios.Salida.Puertos;

/// <summary>
/// Eventos futuros del Silo cuyo instante se deriva de una hora local y una zona. Las entidades vuelven rastreadas: los
/// cambios se confirman con <c>IUnidadTrabajo</c>.
/// </summary>
public interface IHorariosFuturos
{
    /// <summary>Solicitudes activas con servicio desde <paramref name="desde"/>, con sus asignaciones vigentes.</summary>
    Task<IReadOnlyList<(SolicitudViaje Solicitud, IReadOnlyList<Asignacion> Vigentes)>> SolicitudesAsync(DateTimeOffset desde, CancellationToken ct);

    /// <summary>Turnos vigentes que terminan desde <paramref name="desde"/>.</summary>
    Task<IReadOnlyList<Turno>> TurnosAsync(DateTimeOffset desde, CancellationToken ct);

    /// <summary>Paradas sin llegada registrada con hora estimada desde <paramref name="desde"/>.</summary>
    Task<IReadOnlyList<Parada>> ParadasAsync(DateTimeOffset desde, CancellationToken ct);
}
