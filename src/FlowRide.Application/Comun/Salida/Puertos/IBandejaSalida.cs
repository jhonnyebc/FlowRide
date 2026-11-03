namespace FlowRide.Application.Comun.Salida.Puertos;

/// <summary>
/// Transactional Outbox (ADR-0007): deja un evento para publicarlo después, en la misma transacción que el cambio que lo
/// produce. Lo confirma <see cref="IUnidadTrabajo"/>; el worker de notificaciones lo procesa una sola vez (Fase 8).
/// </summary>
public interface IBandejaSalida
{
    Task AgregarAsync(string tipoEvento, object contenido, CancellationToken ct);

    /// <summary>Evento futuro: no se procesa antes de <paramref name="cuando"/> (recordatorios, ADR-0015).</summary>
    Task ProgramarAsync(string tipoEvento, object contenido, DateTimeOffset cuando, CancellationToken ct);
}

/// <summary>Tipos de evento de la Outbox.</summary>
public static class EventosSalida
{
    public const string AsignacionCreada = "asignacion.creada";
    public const string AsignacionReasignada = "asignacion.reasignada";
    public const string AsignacionCancelada = "asignacion.cancelada";
    public const string SolicitudSinCandidatos = "solicitud.sin_candidatos";
    public const string SolicitudCancelada = "solicitud.cancelada";
    public const string TurnoAbandonado = "turno.abandonado";
    public const string AsignacionAceptada = "asignacion.aceptada";
    public const string AsignacionRechazada = "asignacion.rechazada";
    public const string ViajeIniciado = "viaje.iniciado";
    public const string ViajeFinalizado = "viaje.finalizado";
    public const string IncidenteReportado = "incidente.reportado";

    /// <summary>Recordatorio del servicio (RF-35), programado para N minutos antes.</summary>
    public const string RecordatorioServicio = "recordatorio.servicio";

    /// <summary>Un mensaje de WhatsApp no se pudo entregar (error permanente).</summary>
    public const string EnvioWhatsAppFallido = "canal.envio_fallido";
}
