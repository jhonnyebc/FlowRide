using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Asignaciones.Repositorios;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Asignaciones.CasosUso;

/// <summary>Operador al que sirve el stack: lo necesita la configuración del motor (ADR-0009, decision log por tenant).</summary>
public sealed record OperadorDelStack(string TenantId);

public sealed record ResultadoAsignacion(
    Guid SolicitudId,
    AssignmentOutcome? Resultado,
    Guid? AsignacionId,
    Guid? ConductorId,
    Guid? VehiculoId,
    Guid? DecisionId,
    string Motivo);

/// <summary>
/// Lo que el motor necesita saber de la operación en la franja de una solicitud: candidatos, ocupación y carga reciente.
/// Compartido por la asignación automática y la manual, para que el motor las evalúe igual.
/// </summary>
public sealed class PreparadorDecision(IConductorRepositorio conductores, IVehiculoRepositorio vehiculos, IAsignacionRepositorio asignaciones)
{
    public async Task<AgendaOperacion> AgendaAsync(SolicitudViaje solicitud, IReadOnlyCollection<Guid> conductorIds, int ventanaDias,
        DateTimeOffset ahora, Guid? excluirAsignacionId, CancellationToken ct)
    {
        var franja = solicitud.Franja();
        var conductoresOcupados = new HashSet<Guid>(await asignaciones.ConsultarConductoresOcupadosAsync(franja, ct));
        var vehiculosOcupados = new HashSet<Guid>(await asignaciones.ConsultarVehiculosOcupadosAsync(franja, ct));

        // En una reasignación, la asignación que se reemplaza no cuenta como ocupación.
        if (excluirAsignacionId is { } id && await asignaciones.ConsultarPorIdAsync(id, ct) is { } actual)
        {
            conductoresOcupados.Remove(actual.ConductorId);
            vehiculosOcupados.Remove(actual.VehiculoId);
        }

        var recientes = await asignaciones.ContarRecientesAsync(conductorIds, ahora.AddDays(-ventanaDias), ct);
        return new AgendaOperacion(conductoresOcupados, vehiculosOcupados, recientes);
    }

    public Task<IReadOnlyList<Conductor>> ConductoresConTurnoAsync(SolicitudViaje solicitud, CancellationToken ct) =>
        conductores.ConsultarConTurnoQueCubreAsync(solicitud.Franja(), ct);

    public Task<IReadOnlyList<Vehiculo>> VehiculosAsync(CancellationToken ct) => vehiculos.ConsultarTodosAsync(ct);
}

/// <summary>
/// Asignación automática de una solicitud pendiente (RF-10, RF-11, RF-13): arma los candidatos, ejecuta el motor con la
/// configuración activa del operador y, en una sola transacción, guarda el decision log, crea la asignación (o marca la
/// solicitud sin candidatos) y deja el evento en la Outbox (ADR-0007). Si otra réplica ganó la carrera, la unidad de
/// trabajo lanza <see cref="ConflictoConcurrenciaExcepcion"/> y nada se guarda.
/// </summary>
public sealed class AsignarSolicitudCasoUso(
    ISolicitudRepositorio solicitudes,
    IConfiguracionAsignacionRepositorio configuraciones,
    IConductorRepositorio conductores,
    IAsignacionRepositorio asignaciones,
    IDecisionAsignacionRepositorio decisiones,
    PreparadorDecision preparador,
    AssignmentEngine motor,
    OperadorDelStack operador,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<AsignarSolicitudDominio, ResultadoAsignacion>
{
    public async Task<ResultadoAsignacion> EjecutarAsync(AsignarSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var solicitud = await solicitudes.ConsultarPorIdAsync(datos.SolicitudId, ct)
                        ?? throw new FlowRideExcepcion($"La solicitud {datos.SolicitudId} no existe.");
        if (solicitud.Estado != EstadoSolicitud.Pendiente)
            return new ResultadoAsignacion(solicitud.Id, null, null, null, null, null, $"La solicitud está {solicitud.Estado}: no está en la cola.");

        var configuracion = await configuraciones.ConsultarActivaAsync(ct)
                            ?? throw new InvalidOperationException("El operador no tiene una configuración del motor activa.");
        var ventana = configuracion.ParametroEntero(WorkloadBalanceRule.RuleName, ConfiguracionInicial.ParametroVentanaDias)
                      ?? ConfiguracionInicial.VentanaDiasPorDefecto;

        var conductoresConTurno = await preparador.ConductoresConTurnoAsync(solicitud, ct);
        var agenda = await preparador.AgendaAsync(solicitud, conductoresConTurno.Select(c => c.Id).ToList(), ventana, ahora, null, ct);
        var candidatos = AsignacionDominio.Candidatos(solicitud, conductoresConTurno, await preparador.VehiculosAsync(ct), agenda);
        var contexto = new AssignmentContext(solicitud, candidatos, configuracion.ComoConfiguracionDelMotor(operador.TenantId), ahora);
        var resultado = motor.Decide(contexto);

        var decision = DecisionAsignacion.Desde(Guid.CreateVersion7(ahora), configuracion.Id, OrigenAsignacion.Automatica, contexto,
            resultado.DecisionLog);
        await decisiones.CrearAsync(decision, ct);

        Asignacion? asignacion = null;
        switch (resultado.Outcome)
        {
            case AssignmentOutcome.Assigned:
                var elegido = resultado.Selected!;
                asignacion = new Asignacion(Guid.CreateVersion7(ahora), solicitud.Id, elegido.Conductor.Id, elegido.Vehiculo.Id,
                    solicitud.Franja(), OrigenAsignacion.Automatica, ahora, decision.Id);
                await asignaciones.CrearAsync(asignacion, ct);
                solicitud.MarcarAsignada(asignacion, null, ahora);
                await RegistrarEnConductorAsync(elegido.Conductor.Id, ahora, ct);
                await bandeja.AgregarAsync(EventosSalida.AsignacionCreada, Evento(asignacion), ct);
                break;

            case AssignmentOutcome.NoCandidates:
                solicitud.MarcarSinCandidatos(resultado.Reason, ahora);
                await bandeja.AgregarAsync(EventosSalida.SolicitudSinCandidatos,
                    new { solicitudId = solicitud.Id, motivo = resultado.Reason, urgente = solicitud.Urgente }, ct);
                break;

            default:
                // Configuración inválida: queda registrada la decisión y la solicitud sigue en la cola.
                break;
        }

        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return new ResultadoAsignacion(solicitud.Id, resultado.Outcome, asignacion?.Id, asignacion?.ConductorId, asignacion?.VehiculoId,
            decision.Id, resultado.Reason);
    }

    private async Task RegistrarEnConductorAsync(Guid conductorId, DateTimeOffset ahora, CancellationToken ct)
    {
        // Alimenta el desempate "mayor tiempo desde la última asignación" (ADR-0009).
        var conductor = await conductores.ConsultarPorIdAsync(conductorId, ct);
        conductor?.RegistrarAsignacion(ahora);
    }

    internal static object Evento(Asignacion asignacion) => new
    {
        asignacionId = asignacion.Id,
        solicitudId = asignacion.SolicitudId,
        conductorId = asignacion.ConductorId,
        vehiculoId = asignacion.VehiculoId,
        inicio = asignacion.Franja.Inicio,
        fin = asignacion.Franja.Fin,
        origen = asignacion.Origen.ToString()
    };
}

/// <summary>Cola de asignación: solicitudes pendientes en el orden en que se atienden (urgentes primero, RF-13).</summary>
public sealed class ConsultarColaCasoUso(ISolicitudRepositorio solicitudes) : ICasoUsoConRetorno<ConsultarColaDominio, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> EjecutarAsync(ConsultarColaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return solicitudes.ConsultarIdsPendientesAsync(Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

/// <summary>
/// Validación de un candidato manual con el mismo motor (RF-12): solo se acepta si pasa las reglas eliminatorias. La
/// decisión queda en el decision log como manual.
/// </summary>
public sealed class ValidadorCandidatoManual(
    IConfiguracionAsignacionRepositorio configuraciones,
    IConductorRepositorio conductores,
    IVehiculoRepositorio vehiculos,
    IDecisionAsignacionRepositorio decisiones,
    PreparadorDecision preparador,
    AssignmentEngine motor,
    OperadorDelStack operador)
{
    public async Task<(Conductor Conductor, DecisionAsignacion Decision)> ValidarAsync(
        SolicitudViaje solicitud, Guid conductorId, Guid vehiculoId, Guid? asignacionReemplazada, DateTimeOffset ahora, CancellationToken ct)
    {
        var configuracion = await configuraciones.ConsultarActivaAsync(ct)
                            ?? throw new InvalidOperationException("El operador no tiene una configuración del motor activa.");
        var conductor = await conductores.ConsultarPorIdAsync(conductorId, ct)
                        ?? throw new FlowRideExcepcion($"El conductor {conductorId} no existe.");
        var vehiculo = await vehiculos.ConsultarPorIdAsync(vehiculoId, ct)
                       ?? throw new FlowRideExcepcion($"El vehículo {vehiculoId} no existe.");

        var ventana = configuracion.ParametroEntero(WorkloadBalanceRule.RuleName, ConfiguracionInicial.ParametroVentanaDias)
                      ?? ConfiguracionInicial.VentanaDiasPorDefecto;
        var agenda = await preparador.AgendaAsync(solicitud, [conductor.Id], ventana, ahora, asignacionReemplazada, ct);
        var contexto = new AssignmentContext(solicitud, [AsignacionDominio.CandidatoManual(solicitud, conductor, vehiculo, agenda)],
            configuracion.ComoConfiguracionDelMotor(operador.TenantId), ahora);
        var resultado = motor.Decide(contexto);
        AsignacionDominio.ValidarCandidatoManual(resultado);

        var decision = DecisionAsignacion.Desde(Guid.CreateVersion7(ahora), configuracion.Id, OrigenAsignacion.Manual, contexto,
            resultado.DecisionLog);
        await decisiones.CrearAsync(decision, ct);
        return (conductor, decision);
    }
}

/// <summary>
/// Reasignación manual por contingencia (RF-12, OPLOG-AUD-02): la asignación vigente queda Reemplazada y nace una manual con
/// otro conductor o vehículo, que debe pasar las reglas eliminatorias. Con motivo y auditoría.
/// </summary>
public sealed class ReasignarCasoUso(
    IAsignacionRepositorio asignaciones,
    ISolicitudRepositorio solicitudes,
    ValidadorCandidatoManual validador,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ReasignarDominio, Asignacion>
{
    public async Task<Asignacion> EjecutarAsync(ReasignarDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Motivos.Exigir(datos.Motivo);
        var ahora = reloj.GetUtcNow();
        var actual = await asignaciones.ConsultarPorIdAsync(datos.AsignacionId, ct)
                     ?? throw new FlowRideExcepcion($"La asignación {datos.AsignacionId} no existe.");
        if (actual.Estado is not (EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada))
            throw new FlowRideExcepcion($"La asignación está {actual.Estado}: solo se reasigna una asignada o aceptada.");
        if (actual.ConductorId == datos.ConductorId && actual.VehiculoId == datos.VehiculoId)
            throw new FlowRideExcepcion("La reasignación debe cambiar el conductor o el vehículo.");

        var solicitud = await solicitudes.ConsultarPorIdAsync(actual.SolicitudId, ct)
                        ?? throw new InvalidOperationException("La asignación no tiene solicitud.");
        var (conductor, decision) = await validador.ValidarAsync(solicitud, datos.ConductorId, datos.VehiculoId, actual.Id, ahora, ct);

        var nueva = actual.Reemplazar(Guid.CreateVersion7(ahora), datos.ConductorId, datos.VehiculoId, datos.Motivo, ahora, decision.Id);
        await asignaciones.CrearAsync(nueva, ct);
        conductor.RegistrarAsignacion(ahora);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Reasignar), AccionesAuditoria.RecursoAsignacion,
            nueva.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, datos.Motivo,
            Auditar.Valores(("asignacion_id", actual.Id), ("conductor_id", actual.ConductorId), ("vehiculo_id", actual.VehiculoId)),
            Auditar.Valores(("asignacion_id", nueva.Id), ("conductor_id", nueva.ConductorId), ("vehiculo_id", nueva.VehiculoId))), ct);
        await bandeja.AgregarAsync(EventosSalida.AsignacionReasignada,
            new { anterior = AsignarSolicitudCasoUso.Evento(actual), nueva = AsignarSolicitudCasoUso.Evento(nueva), motivo = datos.Motivo }, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return nueva;
    }
}

/// <summary>
/// Asignación manual de una solicitud pendiente o sin candidatos (RF-12): el Operador logístico elige conductor y
/// vehículo, que deben pasar las reglas eliminatorias.
/// </summary>
public sealed class AsignarManualCasoUso(
    ISolicitudRepositorio solicitudes,
    IAsignacionRepositorio asignaciones,
    ValidadorCandidatoManual validador,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<AsignarManualDominio, Asignacion>
{
    public async Task<Asignacion> EjecutarAsync(AsignarManualDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Motivos.Exigir(datos.Motivo);
        var ahora = reloj.GetUtcNow();
        var solicitud = await solicitudes.ConsultarPorIdAsync(datos.SolicitudId, ct)
                        ?? throw new FlowRideExcepcion($"La solicitud {datos.SolicitudId} no existe.");
        if (solicitud.Estado is not (EstadoSolicitud.Pendiente or EstadoSolicitud.SinCandidatos))
            throw new FlowRideExcepcion($"La solicitud está {solicitud.Estado}: solo se asigna a mano una pendiente o sin candidatos.");

        var (conductor, decision) = await validador.ValidarAsync(solicitud, datos.ConductorId, datos.VehiculoId, null, ahora, ct);
        if (solicitud.Estado == EstadoSolicitud.SinCandidatos)
            solicitud.VolverAPendiente(datos.Motivo, datos.ActorId, ahora);

        var asignacion = new Asignacion(Guid.CreateVersion7(ahora), solicitud.Id, datos.ConductorId, datos.VehiculoId, solicitud.Franja(),
            OrigenAsignacion.Manual, ahora, decision.Id);
        await asignaciones.CrearAsync(asignacion, ct);
        solicitud.MarcarAsignada(asignacion, datos.ActorId, ahora);
        conductor.RegistrarAsignacion(ahora);
        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.AsignarManual), AccionesAuditoria.RecursoAsignacion,
            asignacion.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, datos.Motivo,
            valoresDespues: Auditar.Valores(("solicitud_id", solicitud.Id), ("conductor_id", asignacion.ConductorId), ("vehiculo_id", asignacion.VehiculoId))), ct);
        await bandeja.AgregarAsync(EventosSalida.AsignacionCreada, AsignarSolicitudCasoUso.Evento(asignacion), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return asignacion;
    }
}

/// <summary>Devuelve a la cola una solicitud sin candidatos para que el motor la intente de nuevo (RF-12).</summary>
public sealed class DevolverAColaCasoUso(
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<DevolverAColaDominio, SolicitudViaje>
{
    public async Task<SolicitudViaje> EjecutarAsync(DevolverAColaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Motivos.Exigir(datos.Motivo);
        var ahora = reloj.GetUtcNow();
        var solicitud = await solicitudes.ConsultarPorIdAsync(datos.SolicitudId, ct)
                        ?? throw new FlowRideExcepcion($"La solicitud {datos.SolicitudId} no existe.");
        if (solicitud.Estado != EstadoSolicitud.SinCandidatos)
            throw new FlowRideExcepcion($"La solicitud está {solicitud.Estado}: solo vuelve a la cola una sin candidatos.");

        solicitud.VolverAPendiente(datos.Motivo, datos.ActorId, ahora);
        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.DevolverACola), AccionesAuditoria.RecursoSolicitud,
            solicitud.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, datos.Motivo,
            Auditar.Valores(("estado", EstadoSolicitud.SinCandidatos)), Auditar.Valores(("estado", solicitud.Estado))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return solicitud;
    }
}

/// <summary>
/// Un conductor abandona su turno (contingencia del caso ancla, RF-12): el turno queda Abandonado y sus asignaciones aún
/// no iniciadas se cancelan y sus solicitudes vuelven a la cola para que el motor las redistribuya.
/// </summary>
public sealed class AbandonarTurnoCasoUso(
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IAsignacionRepositorio asignaciones,
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<AbandonarTurnoDominio, IReadOnlyList<Guid>>
{
    public async Task<IReadOnlyList<Guid>> EjecutarAsync(AbandonarTurnoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        Motivos.Exigir(datos.Motivo);
        var ahora = reloj.GetUtcNow();
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);
        var turno = conductor.Turnos.SingleOrDefault(t => t.Id == datos.TurnoId)
                    ?? throw new FlowRideExcepcion($"El conductor no tiene el turno {datos.TurnoId}.");
        try
        {
            conductor.AbandonarTurno(turno, datos.Motivo);
        }
        catch (InvalidOperationException error)
        {
            throw new FlowRideExcepcion(error.Message, error);
        }

        var devueltas = new List<Guid>();
        foreach (var asignacion in await asignaciones.ConsultarVigentesDelConductorAsync(conductor.Id, turno.Franja, ct))
        {
            if (asignacion.Estado is not (EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada))
                continue; // Un viaje en curso no se interrumpe.

            asignacion.Cancelar($"Turno abandonado: {datos.Motivo.Trim()}");
            if (await solicitudes.ConsultarPorIdAsync(asignacion.SolicitudId, ct) is { Estado: EstadoSolicitud.Asignada } solicitud)
            {
                solicitud.VolverAPendiente($"Turno del conductor abandonado: {datos.Motivo.Trim()}", datos.ActorId, ahora);
                await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
                devueltas.Add(solicitud.Id);
            }
        }

        await conductores.ModificarAsync(conductor.Id, conductor, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoTurno, AccionesAuditoria.Abandonar), AccionesAuditoria.RecursoTurno,
            turno.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, datos.Motivo,
            valoresDespues: Auditar.Valores(("conductor_id", conductor.Id), ("solicitudes_devueltas", devueltas.Count))), ct);
        await bandeja.AgregarAsync(EventosSalida.TurnoAbandonado,
            new { conductorId = conductor.Id, turnoId = turno.Id, solicitudes = devueltas, motivo = datos.Motivo }, ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return devueltas;
    }
}

internal static class Motivos
{
    public static void Exigir(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new FlowRideExcepcion("El motivo es obligatorio.");
    }
}
