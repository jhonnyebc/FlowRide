using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.Viajes;
using FlowRide.Domain.Viajes.GestionViajes;
using FlowRide.Domain.Viajes.Repositorios;

namespace FlowRide.Application.Viajes.CasosUso;

/// <summary>
/// El conductor acepta la asignación (RF-34): nace su viaje programado. En la Fase 6 lo ejecuta el Operador logístico en
/// su nombre desde el panel; en la Fase 7, el canal WhatsApp.
/// </summary>
public sealed class AceptarAsignacionCasoUso(
    IAsignacionRepositorio asignaciones,
    IViajeRepositorio viajes,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<AceptarAsignacionDominio, Viaje>
{
    public async Task<Viaje> EjecutarAsync(AceptarAsignacionDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var asignacion = await asignaciones.ConsultarPorIdAsync(datos.AsignacionId, ct)
                         ?? throw new FlowRideExcepcion($"La asignación {datos.AsignacionId} no existe.");

        var viaje = ReglasViaje.Aplicar(() => asignacion.Aceptar(Guid.CreateVersion7(ahora)));
        await viajes.CrearAsync(viaje, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Aceptar,
            asignacion.Id, ahora, despues: Auditar.Valores(("estado", asignacion.Estado), ("viaje_id", viaje.Id))), ct);
        await bandeja.AgregarAsync(EventosSalida.AsignacionAceptada, new
        {
            asignacionId = asignacion.Id,
            solicitudId = asignacion.SolicitudId,
            conductorId = asignacion.ConductorId,
            viajeId = viaje.Id
        }, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return viaje;
    }
}

/// <summary>
/// Rechazo de una asignación, del conductor o por vencimiento: la asignación queda Rechazada, la solicitud vuelve a la cola
/// y el motor no se la vuelve a proponer a ese conductor (decisión del 2026-10-18).
/// </summary>
public sealed class DevolucionPorRechazo(
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja)
{
    public async Task AplicarAsync(Asignacion asignacion, string motivo, Guid? actorId, string accion, DateTimeOffset ahora, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asignacion);
        ReglasViaje.Aplicar(() => asignacion.Rechazar(motivo));

        var solicitud = await solicitudes.ConsultarPorIdAsync(asignacion.SolicitudId, ct)
                        ?? throw new InvalidOperationException("La asignación no tiene solicitud.");
        if (solicitud.Estado == EstadoSolicitud.Asignada)
        {
            solicitud.VolverAPendiente($"Asignación rechazada: {motivo.Trim()}", actorId, ahora);
            await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        }

        await auditoria.CrearAsync(new EventoAuditoria(actorId, AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, accion),
            AccionesAuditoria.RecursoAsignacion, asignacion.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, motivo.Trim(),
            Auditar.Valores(("estado", EstadoAsignacion.Asignada)),
            Auditar.Valores(("estado", asignacion.Estado), ("conductor_id", asignacion.ConductorId))), ct);
        await bandeja.AgregarAsync(EventosSalida.AsignacionRechazada, new
        {
            asignacionId = asignacion.Id,
            solicitudId = asignacion.SolicitudId,
            conductorId = asignacion.ConductorId,
            motivo = motivo.Trim(),
            vencida = accion == AccionesAuditoria.Vencer
        }, ct);
    }
}

/// <summary>El conductor rechaza la asignación (RF-34), con motivo.</summary>
public sealed class RechazarAsignacionCasoUso(
    IAsignacionRepositorio asignaciones,
    DevolucionPorRechazo devolucion,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RechazarAsignacionDominio, Asignacion>
{
    public async Task<Asignacion> EjecutarAsync(RechazarAsignacionDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (string.IsNullOrWhiteSpace(datos.Motivo))
            throw new FlowRideExcepcion("El motivo del rechazo es obligatorio.");

        var ahora = reloj.GetUtcNow();
        var asignacion = await asignaciones.ConsultarPorIdAsync(datos.AsignacionId, ct)
                         ?? throw new FlowRideExcepcion($"La asignación {datos.AsignacionId} no existe.");
        await devolucion.AplicarAsync(asignacion, datos.Motivo, datos.ActorId, AccionesAuditoria.Rechazar, ahora, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return asignacion;
    }
}

/// <summary>Asignaciones cuyo conductor no respondió dentro del plazo (parámetro), las más viejas primero.</summary>
public sealed class ConsultarSinRespuestaCasoUso(IAsignacionRepositorio asignaciones, ViajeDominio viajeDominio, TimeProvider reloj)
    : ICasoUsoConRetorno<ConsultarSinRespuestaDominio, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> EjecutarAsync(ConsultarSinRespuestaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var plazo = await viajeDominio.PlazoRespuestaAsync(ct);
        return await asignaciones.ConsultarSinRespuestaAsync(reloj.GetUtcNow() - plazo, Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

/// <summary>
/// Vencimiento (decisión del 2026-10-18): si el conductor no respondió en el plazo, se trata como un rechazo del sistema.
/// Devuelve false si la asignación ya no estaba esperando respuesta (otra réplica o el conductor se adelantaron).
/// </summary>
public sealed class VencerAsignacionCasoUso(
    IAsignacionRepositorio asignaciones,
    ViajeDominio viajeDominio,
    DevolucionPorRechazo devolucion,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<VencerAsignacionDominio, bool>
{
    public async Task<bool> EjecutarAsync(VencerAsignacionDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var plazo = await viajeDominio.PlazoRespuestaAsync(ct);
        if (await asignaciones.ConsultarPorIdAsync(datos.AsignacionId, ct) is not { } asignacion
            || !ReglasViaje.RespuestaVencida(asignacion, plazo, ahora))
            return false;

        await devolucion.AplicarAsync(asignacion, ReglasViaje.MotivoVencimiento(plazo), null, AccionesAuditoria.Vencer, ahora, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return true;
    }
}

/// <summary>El conductor inicia el servicio: viaje y asignación en curso, solicitud en curso.</summary>
public sealed class IniciarViajeCasoUso(
    ViajeDominio viajeDominio,
    IAsignacionRepositorio asignaciones,
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<IniciarViajeDominio, Viaje>
{
    public async Task<Viaje> EjecutarAsync(IniciarViajeDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);
        var (asignacion, solicitud) = await CicloViaje.CargarAsync(viaje, asignaciones, solicitudes, ct);

        ReglasViaje.Aplicar(() => asignacion.Iniciar(viaje, ahora));
        ReglasViaje.Aplicar(() => solicitud.MarcarEnCurso(ahora));
        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoViaje, AccionesAuditoria.Iniciar, viaje.Id, ahora,
            despues: Auditar.Valores(("estado", viaje.Estado), ("inicio", viaje.FechaHoraInicio))), ct);
        await bandeja.AgregarAsync(EventosSalida.ViajeIniciado, CicloViaje.Evento(viaje, asignacion, solicitud), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return viaje;
    }
}

/// <summary>El conductor termina el servicio: viaje finalizado y solicitud completada; el pasajero ya puede calificar.</summary>
public sealed class FinalizarViajeCasoUso(
    ViajeDominio viajeDominio,
    IAsignacionRepositorio asignaciones,
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<FinalizarViajeDominio, Viaje>
{
    public async Task<Viaje> EjecutarAsync(FinalizarViajeDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);
        var (asignacion, solicitud) = await CicloViaje.CargarAsync(viaje, asignaciones, solicitudes, ct);

        ReglasViaje.Aplicar(() => asignacion.Finalizar(viaje, ahora));
        ReglasViaje.Aplicar(() => solicitud.MarcarCompletada(ahora));
        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoViaje, AccionesAuditoria.Finalizar, viaje.Id, ahora,
            despues: Auditar.Valores(("estado", viaje.Estado), ("fin", viaje.FechaHoraFin))), ct);
        await bandeja.AgregarAsync(EventosSalida.ViajeFinalizado, CicloViaje.Evento(viaje, asignacion, solicitud), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return viaje;
    }
}

/// <summary>Parada intermedia de un viaje programado o en curso (orden único dentro del viaje).</summary>
public sealed class RegistrarParadaCasoUso(
    ViajeDominio viajeDominio,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarParadaDominio, Parada>
{
    public async Task<Parada> EjecutarAsync(RegistrarParadaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);

        var parada = ReglasViaje.Aplicar(() => viaje.RegistrarParada(Guid.CreateVersion7(ahora), datos.Direccion, datos.Orden, datos.HoraEstimada));
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoViaje, AccionesAuditoria.RegistrarParada, viaje.Id,
            ahora, despues: Auditar.Valores(("parada_id", parada.Id), ("orden", parada.Orden), ("hora_estimada", parada.HoraEstimada))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return parada;
    }
}

/// <summary>Llegada del vehículo a una parada, con el viaje en curso.</summary>
public sealed class RegistrarLlegadaCasoUso(
    ViajeDominio viajeDominio,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarLlegadaDominio, Parada>
{
    public async Task<Parada> EjecutarAsync(RegistrarLlegadaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);

        var parada = ReglasViaje.Aplicar(() => viaje.RegistrarLlegada(datos.ParadaId, ahora));
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoViaje, AccionesAuditoria.RegistrarLlegada, viaje.Id,
            ahora, despues: Auditar.Valores(("parada_id", parada.Id), ("hora_real", parada.HoraReal))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return parada;
    }
}

/// <summary>Novedad del viaje (retraso, falla mecánica, accidente…): queda en el viaje y sale como alerta (Fase 8).</summary>
public sealed class ReportarIncidenteCasoUso(
    ViajeDominio viajeDominio,
    IAsignacionRepositorio asignaciones,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ReportarIncidenteDominio, Incidente>
{
    public async Task<Incidente> EjecutarAsync(ReportarIncidenteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);

        var incidente = ReglasViaje.Aplicar(() =>
            viaje.RegistrarIncidente(Guid.CreateVersion7(ahora), datos.Tipo, datos.Descripcion, datos.ActorId, ahora));
        var asignacion = await asignaciones.ConsultarPorIdAsync(viaje.AsignacionId, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoViaje, AccionesAuditoria.ReportarIncidente, viaje.Id,
            ahora, despues: Auditar.Valores(("incidente_id", incidente.Id), ("tipo", incidente.Tipo))), ct);
        await bandeja.AgregarAsync(EventosSalida.IncidenteReportado, new
        {
            incidenteId = incidente.Id,
            viajeId = viaje.Id,
            solicitudId = asignacion?.SolicitudId,
            conductorId = asignacion?.ConductorId,
            tipo = incidente.Tipo.ToString(),
            descripcion = incidente.Descripcion
        }, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return incidente;
    }
}

/// <summary>
/// Calificación del pasajero al conductor (RF-54, PAS-AUD-03): una por viaje finalizado, dentro del plazo del operador.
/// En la Fase 6 la registra el Operador logístico en nombre del pasajero; en la Fase 7, el canal WhatsApp.
/// </summary>
public sealed class CalificarViajeCasoUso(
    ViajeDominio viajeDominio,
    IAsignacionRepositorio asignaciones,
    ISolicitudRepositorio solicitudes,
    IPasajeroRepositorio pasajeros,
    ICalificacionRepositorio calificaciones,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CalificarViajeDominio, Calificacion>
{
    public async Task<Calificacion> EjecutarAsync(CalificarViajeDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var viaje = await viajeDominio.ValidarExistaAsync(datos.ViajeId, ct);
        var (_, solicitud) = await CicloViaje.CargarAsync(viaje, asignaciones, solicitudes, ct);
        await viajeDominio.ValidarCalificacionAsync(viaje, solicitud, ahora, ct);
        var pasajero = await pasajeros.ConsultarPorIdAsync(solicitud.PasajeroId, ct)
                       ?? throw new InvalidOperationException("La solicitud no tiene pasajero.");

        var calificacion = ReglasViaje.Aplicar(() => pasajero.Calificar(Guid.CreateVersion7(ahora), viaje, datos.Puntaje, datos.Comentario, ahora));
        await calificaciones.CrearAsync(calificacion, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoCalificacion, AccionesAuditoria.Crear,
            calificacion.Id, ahora, despues: Auditar.Valores(("viaje_id", viaje.Id), ("pasajero_id", pasajero.Id), ("puntaje", calificacion.Puntaje))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return calificacion;
    }
}

internal static class CicloViaje
{
    public static async Task<(Asignacion Asignacion, SolicitudViaje Solicitud)> CargarAsync(
        Viaje viaje, IAsignacionRepositorio asignaciones, ISolicitudRepositorio solicitudes, CancellationToken ct)
    {
        var asignacion = await asignaciones.ConsultarPorIdAsync(viaje.AsignacionId, ct)
                         ?? throw new InvalidOperationException("El viaje no tiene asignación.");
        var solicitud = await solicitudes.ConsultarPorIdAsync(asignacion.SolicitudId, ct)
                        ?? throw new InvalidOperationException("La asignación no tiene solicitud.");
        return (asignacion, solicitud);
    }

    public static object Evento(Viaje viaje, Asignacion asignacion, SolicitudViaje solicitud) => new
    {
        viajeId = viaje.Id,
        asignacionId = asignacion.Id,
        solicitudId = solicitud.Id,
        pasajeroId = solicitud.PasajeroId,
        conductorId = asignacion.ConductorId,
        estado = viaje.Estado.ToString(),
        inicio = viaje.FechaHoraInicio,
        fin = viaje.FechaHoraFin
    };
}
