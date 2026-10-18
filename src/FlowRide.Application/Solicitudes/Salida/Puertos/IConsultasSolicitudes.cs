using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;

namespace FlowRide.Application.Solicitudes.Salida.Puertos;

/// <summary>
/// Lado de lectura de las solicitudes (CQRS ligero): proyecciones listas para pantalla, sin pasar por el agregado. Las
/// escrituras van por <c>ISolicitudRepositorio</c> y el dominio.
/// </summary>
public interface IConsultasSolicitudes
{
    Task<SolicitudDetalleLectura?> DetalleAsync(Guid solicitudId, CancellationToken ct);

    Task<PaginaResultado<SolicitudLectura>> BuscarAsync(FiltroSolicitudes filtro, Paginacion paginacion, CancellationToken ct);
}

/// <summary>Una solicitud con los nombres de su pasajero y su empresa. Sin direcciones si el lector no puede verlas.</summary>
public sealed record SolicitudLectura(
    Guid Id,
    Guid PasajeroId,
    string PasajeroNombre,
    Guid? EmpresaClienteId,
    string? EmpresaClienteNombre,
    Guid? ItinerarioId,
    string? DireccionOrigen,
    string? DireccionDestino,
    DateTimeOffset FechaHoraServicio,
    TimeSpan DuracionEstimada,
    int CantidadPasajeros,
    bool Urgente,
    bool EsPersonal,
    CanalSolicitud Canal,
    EstadoSolicitud Estado,
    DateTimeOffset RegistradaEn,
    bool DetalleRestringido = false)
{
    public SolicitudLectura SinDirecciones() => this with { DireccionOrigen = null, DireccionDestino = null, DetalleRestringido = true };
}

public sealed record CambioEstadoLectura(
    EstadoSolicitud? EstadoAnterior,
    EstadoSolicitud EstadoNuevo,
    DateTimeOffset FechaHora,
    Guid? ActorId,
    string? Motivo);

/// <summary>Estado actual y su historial completo (RF-30, PAS-CONF-02).</summary>
public sealed record SolicitudDetalleLectura(SolicitudLectura Solicitud, IReadOnlyList<CambioEstadoLectura> Historial);
