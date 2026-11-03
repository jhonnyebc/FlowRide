namespace FlowRide.Application.Asignaciones.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.

public sealed record CandidatoManualPeticion
{
    public Guid? ConductorId { get; init; }
    public Guid? VehiculoId { get; init; }
    public string? Motivo { get; init; }
}

public sealed record MotivoPeticion
{
    public string? Motivo { get; init; }
}

public sealed record ReglaPeticion
{
    public string? Codigo { get; init; }
    public bool? Habilitada { get; init; }
    public int? Orden { get; init; }
    public decimal? Peso { get; init; }
    public IReadOnlyDictionary<string, string>? Parametros { get; init; }
}

public sealed record ConfiguracionMotorPeticion
{
    public IReadOnlyList<ReglaPeticion>? Reglas { get; init; }
}

public sealed record BusquedaAsignacionesPeticion
{
    public Guid? ConductorId { get; init; }
    public Guid? SolicitudId { get; init; }

    /// <summary>Asignada, Aceptada, Rechazada, EnCurso, Finalizada, Cancelada o Reemplazada.</summary>
    public string? Estado { get; init; }

    public DateTimeOffset? Desde { get; init; }
    public DateTimeOffset? Hasta { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

/// <summary>Resultado: Asignada, SinCandidatos, ConfiguracionInvalida o NoAplica (la solicitud no estaba en la cola).</summary>
public sealed record ResultadoAsignacionRespuesta(
    Guid SolicitudId,
    string Resultado,
    Guid? AsignacionId,
    Guid? ConductorId,
    Guid? VehiculoId,
    Guid? DecisionId,
    string Motivo);

public sealed record AsignacionRespuesta(
    Guid Id,
    Guid SolicitudId,
    Guid ConductorId,
    Guid VehiculoId,
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    string Estado,
    string Origen,
    DateTimeOffset AsignadaEn,
    Guid? DecisionId,
    Guid? AsignacionReemplazadaId);

public sealed record ReglaRespuesta(string Codigo, string Tipo, bool Habilitada, int Orden, decimal Peso, IReadOnlyDictionary<string, string> Parametros);

public sealed record ConfiguracionMotorRespuesta(
    Guid Id,
    int Version,
    string Estado,
    DateTimeOffset CreadaEn,
    DateTimeOffset? ActivadaEn,
    IReadOnlyList<ReglaRespuesta> Reglas);
