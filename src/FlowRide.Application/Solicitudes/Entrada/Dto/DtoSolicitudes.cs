namespace FlowRide.Application.Solicitudes.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.

public sealed record SolicitudPeticion
{
    public Guid? PasajeroId { get; init; }

    /// <summary>Servicio programado: si se indica, origen, destino y horario salen del itinerario.</summary>
    public Guid? ItinerarioId { get; init; }

    public string? DireccionOrigen { get; init; }
    public string? DireccionDestino { get; init; }
    public DateTimeOffset? FechaHoraServicio { get; init; }
    public int? DuracionMinutos { get; init; }
    public int? CantidadPasajeros { get; init; }
    public bool? Urgente { get; init; }
    public bool? EsPersonal { get; init; }
}

public sealed record CancelarSolicitudPeticion
{
    public string? Motivo { get; init; }
}

public sealed record BusquedaSolicitudesPeticion
{
    /// <summary>Pendiente, Asignada, SinCandidatos, EnCurso, Completada o Cancelada.</summary>
    public string? Estado { get; init; }

    public Guid? PasajeroId { get; init; }
    public Guid? EmpresaClienteId { get; init; }
    public DateTimeOffset? Desde { get; init; }
    public DateTimeOffset? Hasta { get; init; }
    public bool? Urgente { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

public sealed record ItinerariosPeticion
{
    public Guid? EmpresaClienteId { get; init; }
    public string? CodigoServicio { get; init; }
    public string? Origen { get; init; }
    public string? Destino { get; init; }
    public TimeOnly? HoraSalida { get; init; }
    public TimeOnly? HoraLlegada { get; init; }
    public DateOnly? FechaDesde { get; init; }

    /// <summary>Si se omite, solo la fecha inicial.</summary>
    public DateOnly? FechaHasta { get; init; }

    /// <summary>Días de la semana en español (lunes … domingo); si se omite, todos.</summary>
    public IReadOnlyList<string>? Dias { get; init; }
}

public sealed record BusquedaItinerariosPeticion
{
    public Guid? EmpresaClienteId { get; init; }
    public string? CodigoServicio { get; init; }
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

/// <summary>
/// Una solicitud. Si <see cref="DetalleRestringido"/>, es personal y el lector no puede ver sus direcciones (PAS-SEG-01).
/// </summary>
public sealed record SolicitudRespuesta(
    Guid Id,
    Guid PasajeroId,
    string PasajeroNombre,
    Guid? EmpresaClienteId,
    string? EmpresaClienteNombre,
    Guid? ItinerarioId,
    string? DireccionOrigen,
    string? DireccionDestino,
    DateTimeOffset FechaHoraServicio,
    int DuracionMinutos,
    int CantidadPasajeros,
    bool Urgente,
    bool EsPersonal,
    string Canal,
    string Estado,
    DateTimeOffset RegistradaEn,
    bool DetalleRestringido);

public sealed record CambioEstadoRespuesta(string? EstadoAnterior, string EstadoNuevo, DateTimeOffset FechaHora, Guid? ActorId, string? Motivo);

public sealed record SolicitudDetalleRespuesta(SolicitudRespuesta Solicitud, IReadOnlyList<CambioEstadoRespuesta> Historial);

public sealed record ItinerarioRespuesta(
    Guid Id,
    Guid EmpresaClienteId,
    string CodigoServicio,
    DateOnly Fecha,
    string Origen,
    string Destino,
    TimeOnly HoraSalida,
    TimeOnly HoraLlegada);
