using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Application.Viajes.Salida.Puertos;

namespace FlowRide.Application.Viajes.Entrada.Puertos;

// actorId: usuario interno autenticado (auditoría). rol: rol del Panel Web, decide la visibilidad de las solicitudes
// personales. En la Fase 6 las acciones del conductor y del pasajero las ejecuta el Operador logístico en su nombre.

public interface IViajesPuertoEntrada
{
    /// <summary>El conductor acepta la asignación: devuelve el viaje programado (RF-34).</summary>
    Task<ViajeRespuesta> AceptarAsignacionAsync(Guid asignacionId, Guid? actorId, CancellationToken ct);

    /// <summary>El conductor rechaza la asignación: la solicitud vuelve a la cola sin ese conductor.</summary>
    Task RechazarAsignacionAsync(Guid asignacionId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct);

    /// <summary>Asignaciones sin respuesta después del plazo (para el worker).</summary>
    Task<IReadOnlyList<Guid>> SinRespuestaAsync(int maximo, CancellationToken ct);

    /// <summary>Vence una asignación sin respuesta; false si ya no esperaba respuesta.</summary>
    Task<bool> VencerAsignacionAsync(Guid asignacionId, CancellationToken ct);

    Task<ViajeRespuesta> IniciarAsync(Guid viajeId, Guid? actorId, CancellationToken ct);

    Task<ViajeRespuesta> FinalizarAsync(Guid viajeId, Guid? actorId, CancellationToken ct);

    Task<ParadaRespuesta> RegistrarParadaAsync(Guid viajeId, ParadaPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<ParadaRespuesta> RegistrarLlegadaAsync(Guid viajeId, Guid paradaId, Guid? actorId, CancellationToken ct);

    Task<IncidenteRespuesta> ReportarIncidenteAsync(Guid viajeId, IncidentePeticion peticion, Guid? actorId, CancellationToken ct);

    Task<CalificacionRespuesta> CalificarAsync(Guid viajeId, CalificacionPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<PaginaRespuesta<ViajeLectura>> BuscarAsync(BusquedaViajesPeticion peticion, string rol, CancellationToken ct);

    Task<ViajeDetalleLectura> ConsultarAsync(Guid viajeId, string rol, CancellationToken ct);

    Task<IReadOnlyList<MonitoreoViajeLectura>> MonitorearAsync(string rol, CancellationToken ct);

    Task<TrazabilidadLectura> TrazabilidadAsync(Guid solicitudId, string rol, CancellationToken ct);
}
