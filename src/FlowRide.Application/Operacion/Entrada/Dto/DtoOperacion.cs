namespace FlowRide.Application.Operacion.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.
// Las fechas sin hora van como "yyyy-MM-dd".

public sealed record EmpresaClientePeticion
{
    public string? Nombre { get; init; }
    public string? Nit { get; init; }
    public string? CorreoContacto { get; init; }
}

public sealed record PasajeroPeticion
{
    public string? Nombre { get; init; }
    public string? TelefonoWhatsApp { get; init; }
    public string? Correo { get; init; }
    public Guid? EmpresaClienteId { get; init; }
    public string? CodigoInterno { get; init; }
}

public sealed record ConductorPeticion
{
    public string? Nombre { get; init; }
    public string? TelefonoWhatsApp { get; init; }
    public string? Correo { get; init; }
    public string? NumeroLicencia { get; init; }
    public string? CategoriaLicencia { get; init; }
    public DateOnly? VencimientoLicencia { get; init; }

    /// <summary>Compatible con servicio crítico (prioridad en solicitudes urgentes). Por defecto, no.</summary>
    public bool? AptoServicioCritico { get; init; }
}

public sealed record EstadoConductorPeticion
{
    /// <summary>Activo, Suspendido o Inactivo.</summary>
    public string? Estado { get; init; }
}

/// <summary>Turno en hora local, sin desfase; la zona IANA es opcional (por defecto, la del operador).</summary>
public sealed record TurnoPeticion
{
    public DateTime? Inicio { get; init; }
    public DateTime? Fin { get; init; }
    public string? ZonaHoraria { get; init; }
}

public sealed record VehiculoPeticion
{
    public string? Placa { get; init; }
    public string? Marca { get; init; }
    public string? Modelo { get; init; }
    public int? Capacidad { get; init; }
    public DateOnly? VencimientoRtm { get; init; }
    public DateOnly? VencimientoSoat { get; init; }
}

public sealed record ConductorHabitualPeticion
{
    /// <summary>Null quita el conductor habitual.</summary>
    public Guid? ConductorId { get; init; }
}

/// <summary>Filtros y página de los listados (RF-38, RF-41).</summary>
public sealed record BusquedaPeticion
{
    public string? Texto { get; init; }
    public bool? Activo { get; init; }
    public string? Estado { get; init; }
    public Guid? EmpresaClienteId { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

/// <summary>Archivo recibido por el adaptador de infraestructura (multipart).</summary>
public sealed record ArchivoPeticion(string NombreArchivo, string TipoContenido, long Tamano, Stream Contenido);

public sealed record DocumentoPeticion
{
    /// <summary>Licencia, Rtm, Soat u Otro.</summary>
    public string? Tipo { get; init; }

    public DateOnly? FechaVencimiento { get; init; }
}

public sealed record PaginaRespuesta<T>(IReadOnlyList<T> Elementos, int Total, int Pagina, int TamanoPagina);

public sealed record EmpresaClienteRespuesta(Guid Id, string Nombre, string Nit, string CorreoContacto, bool Activa);

public sealed record PasajeroRespuesta(
    Guid Id,
    string Nombre,
    string TelefonoWhatsApp,
    string? Correo,
    Guid EmpresaClienteId,
    string CodigoInterno,
    bool Activo);

public sealed record ConductorRespuesta(
    Guid Id,
    string Nombre,
    string TelefonoWhatsApp,
    string? Correo,
    string NumeroLicencia,
    string CategoriaLicencia,
    DateOnly VencimientoLicencia,
    string Estado,
    bool Activo,
    bool AptoServicioCritico);

public sealed record TurnoRespuesta(
    Guid Id, DateTimeOffset Inicio, DateTimeOffset Fin, string Estado, DateTime InicioLocal, DateTime FinLocal, string ZonaHoraria);

public sealed record DocumentoConductorRespuesta(Guid Id, string Tipo, DateOnly? FechaVencimiento);

public sealed record ConductorDetalleRespuesta(
    ConductorRespuesta Conductor,
    IReadOnlyList<TurnoRespuesta> Turnos,
    IReadOnlyList<DocumentoConductorRespuesta> Documentos);

public sealed record UrlDocumentoRespuesta(Uri Url, DateTimeOffset ExpiraEn);

public sealed record ConductorDisponibleRespuesta(Guid Id, string Nombre, string CategoriaLicencia, DateOnly VencimientoLicencia);

public sealed record VehiculoRespuesta(
    Guid Id,
    string Placa,
    string Marca,
    string Modelo,
    int Capacidad,
    DateOnly VencimientoRtm,
    DateOnly VencimientoSoat,
    bool Activo,
    Guid? ConductorHabitualId);
