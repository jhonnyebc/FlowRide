namespace FlowRide.Application.Viajes.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.

public sealed record ParadaPeticion
{
    public string? Direccion { get; init; }
    public int? Orden { get; init; }
    /// <summary>Hora local estimada, sin desfase; la zona IANA es opcional (por defecto, la del operador).</summary>
    public DateTime? HoraEstimada { get; init; }

    public string? ZonaHoraria { get; init; }
}

public sealed record IncidentePeticion
{
    /// <summary>Retraso, FallaMecanica, AbandonoTurno, Accidente u Otro.</summary>
    public string? Tipo { get; init; }

    public string? Descripcion { get; init; }
}

public sealed record CalificacionPeticion
{
    /// <summary>De 1 a 5.</summary>
    public int? Puntaje { get; init; }

    public string? Comentario { get; init; }
}

public sealed record BusquedaViajesPeticion
{
    /// <summary>Programado, EnCurso, Finalizado o Cancelado.</summary>
    public string? Estado { get; init; }

    public Guid? ConductorId { get; init; }
    public Guid? PasajeroId { get; init; }
    public Guid? EmpresaClienteId { get; init; }

    /// <summary>Desde (incluida) y hasta (excluida) la hora programada del servicio.</summary>
    public DateTimeOffset? Desde { get; init; }

    public DateTimeOffset? Hasta { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

public sealed record ViajeRespuesta(Guid Id, Guid AsignacionId, string Estado, DateTimeOffset? FechaHoraInicio, DateTimeOffset? FechaHoraFin);

public sealed record ParadaRespuesta(
    Guid Id, Guid ViajeId, int Orden, string Direccion, DateTimeOffset HoraEstimada, DateTimeOffset? HoraReal, DateTime HoraEstimadaLocal, string ZonaHoraria);

public sealed record IncidenteRespuesta(Guid Id, Guid ViajeId, string Tipo, string Descripcion, DateTimeOffset FechaHora, Guid ReportadoPorId);

public sealed record CalificacionRespuesta(Guid Id, Guid ViajeId, Guid PasajeroId, int Puntaje, string? Comentario, DateTimeOffset FechaHora);
