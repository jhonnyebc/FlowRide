using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.Application.Viajes.Salida.Puertos;

/// <summary>
/// Lado de lectura de los viajes (CQRS ligero): historial (RF-31), monitoreo de la operación (RF-37) y trazabilidad de
/// cada servicio (RF-40). Las escrituras van por <c>IViajeRepositorio</c> y el dominio.
/// </summary>
public interface IConsultasViajes
{
    Task<PaginaResultado<ViajeLectura>> BuscarAsync(FiltroViajes filtro, Paginacion paginacion, CancellationToken ct);

    Task<ViajeDetalleLectura?> DetalleAsync(Guid viajeId, CancellationToken ct);

    /// <summary>Viajes en curso y programados que empiezan antes de <paramref name="programadosHasta"/>, por hora de inicio.</summary>
    Task<IReadOnlyList<ViajeLectura>> ActivosAsync(DateTimeOffset programadosHasta, CancellationToken ct);

    /// <summary>Línea de tiempo del servicio, o null si la solicitud no existe.</summary>
    Task<TrazabilidadLectura?> TrazabilidadAsync(Guid solicitudId, CancellationToken ct);
}

/// <summary>Un viaje con su servicio, conductor, vehículo y pasajero. Sin direcciones si el lector no puede verlas.</summary>
public sealed record ViajeLectura(
    Guid Id,
    Guid AsignacionId,
    Guid SolicitudId,
    EstadoViaje Estado,
    Guid ConductorId,
    string ConductorNombre,
    Guid VehiculoId,
    string Placa,
    Guid PasajeroId,
    string PasajeroNombre,
    Guid? EmpresaClienteId,
    string? EmpresaClienteNombre,
    string? DireccionOrigen,
    string? DireccionDestino,
    bool Urgente,
    bool EsPersonal,
    DateTimeOffset InicioProgramado,
    DateTimeOffset FinProgramado,
    DateTimeOffset? FechaHoraInicio,
    DateTimeOffset? FechaHoraFin,
    int Incidentes,
    int? Puntaje,
    bool DetalleRestringido = false)
{
    public ViajeLectura SinDirecciones() => this with { DireccionOrigen = null, DireccionDestino = null, DetalleRestringido = true };
}

public sealed record ParadaLectura(Guid Id, int Orden, string? Direccion, DateTimeOffset HoraEstimada, DateTimeOffset? HoraReal);

public sealed record IncidenteLectura(Guid Id, TipoIncidente Tipo, string Descripcion, DateTimeOffset FechaHora, Guid ReportadoPorId);

public sealed record CalificacionLectura(Guid Id, int Puntaje, string? Comentario, DateTimeOffset FechaHora);

public sealed record ViajeDetalleLectura(
    ViajeLectura Viaje,
    IReadOnlyList<ParadaLectura> Paradas,
    IReadOnlyList<IncidenteLectura> Incidentes,
    CalificacionLectura? Calificacion)
{
    public ViajeDetalleLectura SinDirecciones() => this with
    {
        Viaje = Viaje.SinDirecciones(),
        Paradas = Paradas.Select(parada => parada with { Direccion = null }).ToList()
    };
}

/// <summary>Un viaje en el monitoreo (RF-37): retrasado si sigue programado después de su hora de inicio.</summary>
public sealed record MonitoreoViajeLectura(ViajeLectura Viaje, bool Retrasado, int MinutosRetraso);

/// <summary>
/// Un hecho del servicio (RF-40). Tipo: <c>solicitud</c>, <c>decision</c>, <c>asignacion</c>, <c>viaje</c>,
/// <c>parada</c>, <c>incidente</c>, <c>calificacion</c> o la acción auditada (<c>recurso.accion</c>). Sin direcciones.
/// </summary>
public sealed record EventoTrazabilidad(DateTimeOffset FechaHora, string Tipo, string Descripcion, Guid? ActorId, string? Motivo, Guid? ReferenciaId);

public sealed record TrazabilidadLectura(Guid SolicitudId, EstadoSolicitud Estado, IReadOnlyList<EventoTrazabilidad> Eventos);
