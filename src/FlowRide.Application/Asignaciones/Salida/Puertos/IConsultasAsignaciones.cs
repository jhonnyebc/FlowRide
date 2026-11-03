using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Application.Asignaciones.Salida.Puertos;

/// <summary>Lado de lectura de las asignaciones y del decision log (CQRS ligero, RF-38).</summary>
public interface IConsultasAsignaciones
{
    Task<PaginaResultado<AsignacionLectura>> BuscarAsync(FiltroAsignaciones filtro, Paginacion paginacion, CancellationToken ct);

    /// <summary>Decisiones del motor sobre la solicitud, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct);
}

public sealed record AsignacionLectura(
    Guid Id,
    Guid SolicitudId,
    Guid ConductorId,
    string ConductorNombre,
    Guid VehiculoId,
    string Placa,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    EstadoAsignacion Estado,
    OrigenAsignacion Origen,
    DateTimeOffset AsignadaEn,
    string? MotivoCierre,
    Guid? AsignacionReemplazadaId);

public sealed record ResultadoReglaLectura(
    string CodigoRegla,
    int VersionRegla,
    string Tipo,
    int Orden,
    bool? Aprobado,
    decimal? Valor,
    decimal? Peso,
    decimal? ValorPonderado,
    string Motivo);

public sealed record EvaluacionLectura(
    Guid ConductorId,
    Guid VehiculoId,
    Guid TurnoId,
    bool Elegible,
    string? MotivoDescarte,
    decimal Prioridad,
    decimal PuntajeTotal,
    int? PosicionRanking,
    IReadOnlyList<ResultadoReglaLectura> Resultados);

public sealed record DecisionLectura(
    Guid Id,
    Guid SolicitudId,
    int VersionConfiguracion,
    OrigenAsignacion Origen,
    DateTimeOffset EvaluadaEn,
    string Resultado,
    Guid? ConductorSeleccionadoId,
    Guid? VehiculoSeleccionadoId,
    string Motivo,
    string? Desempate,
    IReadOnlyList<EvaluacionLectura> Evaluaciones);
