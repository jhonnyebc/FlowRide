using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Asignaciones.GestionAsignaciones;

// Datos de entrada de los casos de uso de asignación (RF-10 a RF-13, RF-21, RF-38). ActorId es el usuario interno; null
// cuando actúa el sistema (el worker de la cola).

public sealed record AsignarSolicitudDominio(Guid SolicitudId);

public sealed record ConsultarColaDominio(int Maximo);

public sealed record ReasignarDominio(Guid AsignacionId, Guid ConductorId, Guid VehiculoId, string Motivo, Guid ActorId);

public sealed record AsignarManualDominio(Guid SolicitudId, Guid ConductorId, Guid VehiculoId, string Motivo, Guid ActorId);

public sealed record DevolverAColaDominio(Guid SolicitudId, string Motivo, Guid ActorId);

public sealed record AbandonarTurnoDominio(Guid ConductorId, Guid TurnoId, string Motivo, Guid? ActorId);

public sealed record CrearConfiguracionDominio(IReadOnlyList<ReglaPedida> Reglas, Guid? ActorId);

public sealed record ActivarConfiguracionDominio(int Version, Guid? ActorId);

public sealed record ConsultarConfiguracionesDominio
{
    public static ConsultarConfiguracionesDominio Todas { get; } = new();
}

public sealed record ConsultarDecisionesDominio(Guid SolicitudId);

public sealed record FiltroAsignaciones(
    Guid? ConductorId = null,
    Guid? SolicitudId = null,
    EstadoAsignacion? Estado = null,
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null);

public sealed record BuscarAsignacionesDominio(FiltroAsignaciones Filtro, Paginacion Paginacion);
