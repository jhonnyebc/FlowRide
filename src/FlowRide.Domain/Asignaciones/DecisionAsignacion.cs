using System.Text.Json;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Asignaciones;

/// <summary>
/// Decision log persistente (ADR-0009, motor.decision_asignacion): todo lo necesario para reconstruir una decisión del
/// motor — entrada (solicitud, candidatos, configuración), resultado de cada regla por candidato, puntajes, desempate y
/// versión de la configuración. Solo se inserta.
/// </summary>
public sealed class DecisionAsignacion
{
    private readonly List<EvaluacionCandidato> _evaluaciones = [];

#pragma warning disable CS8618 // Solo para EF Core.
    private DecisionAsignacion()
    {
    }
#pragma warning restore CS8618

    private DecisionAsignacion(Guid id, Guid configuracionId, OrigenAsignacion origen, AssignmentContext contexto, AssignmentDecisionLog log)
    {
        Id = id;
        SolicitudId = log.SolicitudId;
        ConfiguracionId = configuracionId;
        VersionConfiguracion = log.ConfigurationVersion;
        Origen = origen;
        EvaluadaEn = log.EvaluatedAt;
        Resultado = log.Outcome;
        ConductorSeleccionadoId = log.Selected?.ConductorId;
        VehiculoSeleccionadoId = log.Selected?.VehiculoId;
        Motivo = log.Reason;
        Desempate = log.TieBreak is null ? null : JsonSerializer.Serialize(new
        {
            empatados = log.TieBreak.TiedCandidates.Select(Referencia),
            decididoPor = log.TieBreak.DecidedBy,
            criterio = log.TieBreak.Criterion,
            ganador = Referencia(log.TieBreak.Winner)
        });
        EntradaSnapshot = Snapshot(contexto);

        foreach (var evaluacion in log.Evaluations)
            _evaluaciones.Add(new EvaluacionCandidato(Guid.CreateVersion7(log.EvaluatedAt), id, evaluacion));
    }

    public Guid Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Guid ConfiguracionId { get; private set; }
    public int VersionConfiguracion { get; private set; }

    /// <summary>Automática (el motor eligió) o Manual (el motor validó el candidato del Operador logístico).</summary>
    public OrigenAsignacion Origen { get; private set; }

    public DateTimeOffset EvaluadaEn { get; private set; }
    public AssignmentOutcome Resultado { get; private set; }
    public Guid? ConductorSeleccionadoId { get; private set; }
    public Guid? VehiculoSeleccionadoId { get; private set; }
    public string Motivo { get; private set; }

    /// <summary>JSON del desempate, si lo hubo.</summary>
    public string? Desempate { get; private set; }

    /// <summary>JSON con la entrada completa: solicitud, candidatos y reglas configuradas.</summary>
    public string EntradaSnapshot { get; private set; }

    public IReadOnlyList<EvaluacionCandidato> Evaluaciones => _evaluaciones;

    public static DecisionAsignacion Desde(Guid id, Guid configuracionId, OrigenAsignacion origen, AssignmentContext contexto, AssignmentDecisionLog log)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(log);
        return new DecisionAsignacion(id, configuracionId, origen, contexto, log);
    }

    private static object Referencia(CandidateReference referencia) =>
        new { conductorId = referencia.ConductorId, vehiculoId = referencia.VehiculoId, turnoId = referencia.TurnoId };

    private static string Snapshot(AssignmentContext contexto)
    {
        var solicitud = contexto.Solicitud;
        return JsonSerializer.Serialize(new
        {
            solicitud = new
            {
                id = solicitud.Id,
                fechaHoraServicio = solicitud.FechaHoraServicio,
                duracionMinutos = solicitud.DuracionEstimada.TotalMinutes,
                cantidadPasajeros = solicitud.CantidadPasajeros,
                urgente = solicitud.Urgente
            },
            configuracion = new
            {
                version = contexto.Configuration.Version,
                reglas = contexto.Configuration.Rules.Select(r => new { nombre = r.Name, habilitada = r.Enabled, orden = r.Order, peso = r.Weight })
            },
            candidatos = contexto.Candidatos.Select(c => new
            {
                conductorId = c.Conductor.Id,
                conductorEstado = c.Conductor.Estado.ToString(),
                conductorActivo = c.Conductor.Activo,
                aptoServicioCritico = c.Conductor.AptoServicioCritico,
                vencimientoLicencia = c.Conductor.VencimientoLicencia,
                vehiculoId = c.Vehiculo.Id,
                vehiculoActivo = c.Vehiculo.Activo,
                capacidad = c.Vehiculo.Capacidad,
                turnoId = c.Turno.Id,
                turnoEstado = c.Turno.Estado.ToString(),
                turnoInicio = c.Turno.Inicio,
                turnoFin = c.Turno.Fin,
                asignacionesRecientes = c.AsignacionesRecientes,
                ultimaAsignacion = c.UltimaAsignacion,
                conductorOcupado = c.ConductorOcupado,
                vehiculoOcupado = c.VehiculoOcupado
            })
        });
    }
}

/// <summary>Evaluación de un candidato dentro de una decisión (motor.evaluacion_candidato).</summary>
public sealed class EvaluacionCandidato
{
    private readonly List<ResultadoRegla> _resultados = [];

#pragma warning disable CS8618 // Solo para EF Core.
    private EvaluacionCandidato()
    {
    }
#pragma warning restore CS8618

    internal EvaluacionCandidato(Guid id, Guid decisionId, CandidateEvaluation evaluacion)
    {
        Id = id;
        DecisionId = decisionId;
        ConductorId = evaluacion.Reference.ConductorId;
        VehiculoId = evaluacion.Reference.VehiculoId;
        TurnoId = evaluacion.Reference.TurnoId;
        Elegible = evaluacion.Eligible;
        MotivoDescarte = evaluacion.DiscardReason;
        Prioridad = evaluacion.PriorityScore;
        PuntajeTotal = evaluacion.TotalScore;
        PosicionRanking = evaluacion.Rank;
        foreach (var resultado in evaluacion.RuleOutcomes)
            _resultados.Add(new ResultadoRegla(Guid.NewGuid(), id, resultado));
    }

    public Guid Id { get; private set; }
    public Guid DecisionId { get; private set; }
    public Guid ConductorId { get; private set; }
    public Guid VehiculoId { get; private set; }
    public Guid TurnoId { get; private set; }
    public bool Elegible { get; private set; }
    public string? MotivoDescarte { get; private set; }
    public decimal Prioridad { get; private set; }
    public decimal PuntajeTotal { get; private set; }
    public int? PosicionRanking { get; private set; }
    public IReadOnlyList<ResultadoRegla> Resultados => _resultados;
}

/// <summary>Resultado de una regla sobre un candidato (motor.resultado_regla).</summary>
public sealed class ResultadoRegla
{
#pragma warning disable CS8618 // Solo para EF Core.
    private ResultadoRegla()
    {
    }
#pragma warning restore CS8618

    internal ResultadoRegla(Guid id, Guid evaluacionId, RuleOutcome resultado)
    {
        Id = id;
        EvaluacionId = evaluacionId;
        CodigoRegla = resultado.RuleName;
        VersionRegla = resultado.RuleVersion;
        Tipo = resultado.Kind;
        Orden = resultado.Order;
        Aprobado = resultado.Passed;
        Valor = resultado.RawValue;
        Peso = resultado.Weight;
        ValorPonderado = resultado.WeightedValue;
        Motivo = resultado.Reason;
    }

    public Guid Id { get; private set; }
    public Guid EvaluacionId { get; private set; }
    public string CodigoRegla { get; private set; }
    public int VersionRegla { get; private set; }
    public RuleKind Tipo { get; private set; }
    public int Orden { get; private set; }
    public bool? Aprobado { get; private set; }
    public decimal? Valor { get; private set; }
    public decimal? Peso { get; private set; }
    public decimal? ValorPonderado { get; private set; }
    public string Motivo { get; private set; }
}
