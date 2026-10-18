using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes;

namespace FlowRide.Domain.Viajes.GestionViajes;

// Datos de entrada de los casos de uso de viajes (RF-31, RF-34, RF-37, RF-40, RF-54). ActorId es el usuario interno que
// actúa: en la Fase 6, el Operador logístico desde el panel en nombre del conductor o del pasajero; desde la Fase 7,
// también el canal WhatsApp. Null cuando actúa el sistema (vencimiento de asignaciones).

public sealed record AceptarAsignacionDominio(Guid AsignacionId, Guid ActorId);

public sealed record RechazarAsignacionDominio(Guid AsignacionId, string Motivo, Guid ActorId);

/// <summary>Asignaciones que esperan respuesta del conductor desde antes del plazo (para el worker).</summary>
public sealed record ConsultarSinRespuestaDominio(int Maximo);

public sealed record VencerAsignacionDominio(Guid AsignacionId);

public sealed record IniciarViajeDominio(Guid ViajeId, Guid ActorId);

public sealed record FinalizarViajeDominio(Guid ViajeId, Guid ActorId);

public sealed record RegistrarParadaDominio(Guid ViajeId, string Direccion, int Orden, DateTimeOffset HoraEstimada, Guid ActorId);

public sealed record RegistrarLlegadaDominio(Guid ViajeId, Guid ParadaId, Guid ActorId);

public sealed record ReportarIncidenteDominio(Guid ViajeId, TipoIncidente Tipo, string Descripcion, Guid ActorId);

public sealed record CalificarViajeDominio(Guid ViajeId, int Puntaje, string? Comentario, Guid ActorId);

/// <summary>Filtros del historial de viajes (RF-31): por la franja programada del servicio.</summary>
public sealed record FiltroViajes(
    EstadoViaje? Estado = null,
    Guid? ConductorId = null,
    Guid? PasajeroId = null,
    Guid? EmpresaClienteId = null,
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null);

public sealed record BuscarViajesDominio(FiltroViajes Filtro, Paginacion Paginacion, LectorSolicitud Lector);

public sealed record ConsultarViajeDominio(Guid ViajeId, LectorSolicitud Lector);

public sealed record MonitorearViajesDominio(LectorSolicitud Lector);

public sealed record TrazabilidadSolicitudDominio(Guid SolicitudId, LectorSolicitud Lector);
