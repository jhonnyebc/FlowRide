namespace FlowRide.Application.Comun.Salida.Puertos;

/// <summary>
/// Transactional Outbox (ADR-0007): deja un evento para publicarlo después, en la misma transacción que el cambio que lo
/// produce. Lo confirma <see cref="IUnidadTrabajo"/>; un worker lo procesa de forma idempotente (Fase 8).
/// </summary>
public interface IBandejaSalida
{
    Task AgregarAsync(string tipoEvento, object contenido, CancellationToken ct);
}

/// <summary>Tipos de evento de la Outbox.</summary>
public static class EventosSalida
{
    public const string AsignacionCreada = "asignacion.creada";
    public const string AsignacionReasignada = "asignacion.reasignada";
    public const string AsignacionCancelada = "asignacion.cancelada";
    public const string SolicitudSinCandidatos = "solicitud.sin_candidatos";
    public const string TurnoAbandonado = "turno.abandonado";
}
