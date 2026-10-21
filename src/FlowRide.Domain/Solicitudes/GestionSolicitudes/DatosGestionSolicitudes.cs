using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Solicitudes.GestionSolicitudes;

// Datos de entrada de los casos de uso de solicitudes e itinerarios (RF-08, RF-09, RF-29 a RF-32).

/// <summary>
/// Nueva solicitud. Con <see cref="ItinerarioId"/>, origen, destino, fecha y duración salen del itinerario; sin él, son
/// obligatorios. ActorId es quien la registra (el pasajero por WhatsApp o un usuario del Panel Web).
/// </summary>
public sealed record RegistrarSolicitudDominio(
    Guid PasajeroId,
    Guid? ItinerarioId,
    string? DireccionOrigen,
    string? DireccionDestino,
    FechaHoraLocal? Servicio,
    TimeSpan? DuracionEstimada,
    int CantidadPasajeros,
    bool Urgente,
    bool EsPersonal,
    CanalSolicitud Canal,
    Guid ActorId);

public sealed record ModificarSolicitudDominio(
    Guid SolicitudId,
    string DireccionOrigen,
    string DireccionDestino,
    FechaHoraLocal Servicio,
    TimeSpan DuracionEstimada,
    int CantidadPasajeros,
    bool Urgente,
    Guid ActorId);

/// <summary><see cref="PuedeSaltarVentana"/>: el Operador logístico cancela fuera del plazo por una contingencia.</summary>
public sealed record CancelarSolicitudDominio(Guid SolicitudId, string Motivo, Guid ActorId, bool PuedeSaltarVentana);

public sealed record RegistrarItinerariosDominio(
    Guid EmpresaClienteId,
    string CodigoServicio,
    string Origen,
    string Destino,
    TimeOnly HoraSalida,
    TimeOnly HoraLlegada,
    string ZonaHoraria,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    IReadOnlyCollection<DayOfWeek> Dias,
    Guid? ActorId);

/// <summary>Filtros del historial y la cola de solicitudes (RF-30, RF-31).</summary>
public sealed record FiltroSolicitudes(
    EstadoSolicitud? Estado = null,
    Guid? PasajeroId = null,
    Guid? EmpresaClienteId = null,
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    bool? Urgente = null);

public sealed record ConsultarSolicitudDominio(Guid SolicitudId, LectorSolicitud Lector);

public sealed record BuscarSolicitudesDominio(FiltroSolicitudes Filtro, Comun.Consultas.Paginacion Paginacion, LectorSolicitud Lector);

public sealed record BuscarItinerariosDominio(Repositorios.FiltroItinerarios Filtro, Comun.Consultas.Paginacion Paginacion);
