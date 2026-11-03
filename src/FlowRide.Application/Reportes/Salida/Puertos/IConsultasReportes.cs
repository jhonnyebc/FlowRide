using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Reportes;

namespace FlowRide.Application.Reportes.Salida.Puertos;

/// <summary>
/// Lado de lectura de los reportes (CQRS ligero): filas planas del periodo, listas para agregar (RF-36, RF-42, RF-46 a
/// RF-49). Los viajes salen de <see cref="IConsultasViajes"/>. Siempre contra PostgreSQL, sin caché.
/// </summary>
public interface IConsultasReportes
{
    /// <summary>Solicitudes cuyo servicio cae en el periodo, con su última asignación y su viaje; hasta <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<SolicitudReporteLectura>> SolicitudesAsync(PeriodoReporte periodo, Guid? empresaClienteId, int maximo, CancellationToken ct);

    /// <summary>Asignaciones cuya franja empieza en el periodo, con su viaje; hasta <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<AsignacionReporteLectura>> AsignacionesAsync(PeriodoReporte periodo, Guid? conductorId, int maximo, CancellationToken ct);
}

/// <summary>
/// Caché de indicadores de periodos cerrados (ADR-0006): baja volatilidad, obsolescencia acotada por el TTL y PostgreSQL
/// como fuente de verdad. Nunca guarda direcciones de los servicios.
/// </summary>
public interface ICacheReportes
{
    Task<T> ObtenerOCrearAsync<T>(string clave, Func<CancellationToken, Task<T>> crear, CancellationToken ct) where T : class;
}

/// <summary>Una solicitud con su pasajero, empresa, última asignación y viaje. Sin direcciones si el lector no puede verlas.</summary>
public sealed record SolicitudReporteLectura(
    Guid Id,
    Guid PasajeroId,
    string PasajeroNombre,
    Guid? EmpresaClienteId,
    string? EmpresaClienteNombre,
    string? DireccionOrigen,
    string? DireccionDestino,
    DateTimeOffset FechaHoraServicio,
    int CantidadPasajeros,
    bool Urgente,
    bool EsPersonal,
    CanalSolicitud Canal,
    EstadoSolicitud Estado,
    DateTimeOffset RegistradaEn,
    Guid? AsignacionId,
    EstadoAsignacion? EstadoAsignacion,
    Guid? ConductorId,
    string? ConductorNombre,
    string? Placa,
    Guid? ViajeId,
    EstadoViaje? EstadoViaje,
    DateTimeOffset? InicioReal,
    DateTimeOffset? FinReal,
    int Incidentes,
    int? Puntaje,
    bool DetalleRestringido = false)
{
    public SolicitudReporteLectura SinDirecciones() => this with { DireccionOrigen = null, DireccionDestino = null, DetalleRestringido = true };
}

/// <summary>Una asignación con su conductor, vehículo y viaje. Vencida: el sistema la cerró porque el conductor no respondió.</summary>
public sealed record AsignacionReporteLectura(
    Guid Id,
    Guid SolicitudId,
    Guid ConductorId,
    string ConductorNombre,
    string Placa,
    OrigenAsignacion Origen,
    EstadoAsignacion Estado,
    bool Vencida,
    DateTimeOffset InicioProgramado,
    Guid? ViajeId,
    EstadoViaje? EstadoViaje,
    DateTimeOffset? InicioReal,
    int Incidentes,
    int? Puntaje);

// Resultados de los casos de uso.

/// <summary>Un reporte: su periodo, el resumen de todo el periodo y la página de filas pedida.</summary>
public sealed record ReporteLectura<TResumen, TFila>(DateTimeOffset Desde, DateTimeOffset Hasta, TResumen Resumen, PaginaResultado<TFila> Filas);

/// <summary>Un servicio en la vista centralizada (RF-36) con su nivel de atención y el porqué.</summary>
public sealed record ServicioVistaLectura(SolicitudReporteLectura Servicio, NivelAtencion Nivel, string? Motivo);

/// <summary>Vista centralizada de solicitudes y viajes del día (RF-36): primero lo crítico, luego por hora del servicio.</summary>
public sealed record VistaCentralizadaLectura(
    DateTimeOffset Desde,
    DateTimeOffset Hasta,
    int Criticos,
    int Atencion,
    int Normales,
    IReadOnlyList<ServicioVistaLectura> Servicios);

/// <summary>RF-46: resumen de los viajes realizados.</summary>
public sealed record ResumenViajes(
    int Viajes,
    int Puntuales,
    decimal? PorcentajePuntualidad,
    decimal? RetrasoPromedioMinutos,
    int Incidentes,
    int Calificados,
    decimal? CalificacionPromedio,
    int ToleranciaMinutos);

public sealed record ViajeReporteLectura(ViajeLectura Viaje, bool Puntual, int MinutosRetraso);

/// <summary>
/// RF-47: solicitudes atendidas (en curso o completadas), pendientes (sin conductor o sin iniciar) y canceladas. El
/// porcentaje de atención es sobre las no canceladas.
/// </summary>
public sealed record ResumenSolicitudes(
    int Total,
    int Atendidas,
    int Pendientes,
    int Canceladas,
    decimal? PorcentajeAtencion,
    IReadOnlyDictionary<string, int> PorEstado);

public sealed record SolicitudReporteFila(SolicitudReporteLectura Solicitud, GrupoSolicitud Grupo);

/// <summary>RF-48: totales de las asignaciones del periodo.</summary>
public sealed record ResumenConductores(int Conductores, int Asignaciones, int Aceptadas, int Rechazadas, int Vencidas, int ViajesRealizados);

/// <summary>
/// RF-48: un conductor en el periodo. Aceptadas: las que generaron viaje. Rechazadas no incluye las vencidas. SinRespuesta:
/// todavía esperan que el conductor responda.
/// </summary>
public sealed record ConductorReporteLectura(
    Guid ConductorId,
    string Nombre,
    string Placas,
    int Asignaciones,
    int Aceptadas,
    int Rechazadas,
    int Vencidas,
    int SinRespuesta,
    decimal? PorcentajeAceptacion,
    int ViajesRealizados,
    int Puntuales,
    decimal? PorcentajePuntualidad,
    int Incidentes,
    decimal? CalificacionPromedio);

/// <summary>RF-49: totales del periodo por cliente empresarial.</summary>
public sealed record ResumenEmpresas(int Empresas, int Solicitudes, int ViajesRealizados);

/// <summary>RF-49: un cliente empresarial en el periodo; sin id para los servicios sin empresa.</summary>
public sealed record EmpresaReporteLectura(
    Guid? EmpresaClienteId,
    string Nombre,
    int Solicitudes,
    int Atendidas,
    int Pendientes,
    int Canceladas,
    int Urgentes,
    int Pasajeros,
    int ViajesRealizados,
    int Incidentes,
    decimal? CalificacionPromedio);

/// <summary>Indicadores básicos de un periodo (RF-42).</summary>
public sealed record IndicadoresLectura(
    DateTimeOffset Desde,
    DateTimeOffset Hasta,
    IndicadoresVolumen Volumen,
    IndicadoresCumplimiento Cumplimiento,
    IndicadoresConductores Conductores,
    IReadOnlyList<EmpresaReporteLectura> Empresas);

public sealed record IndicadoresVolumen(
    int Solicitudes,
    int Atendidas,
    int Pendientes,
    int Canceladas,
    int Urgentes,
    IReadOnlyDictionary<string, int> SolicitudesPorEstado,
    int ViajesRealizados,
    int ViajesCancelados);

/// <summary>Puntualidad sobre los viajes que ya iniciaron; incidentes de todos los viajes del periodo.</summary>
public sealed record IndicadoresCumplimiento(
    int ViajesIniciados,
    int Puntuales,
    decimal? PorcentajePuntualidad,
    decimal? RetrasoPromedioMinutos,
    int Incidentes,
    int ToleranciaMinutos);

/// <summary>Tasas sobre las asignaciones que ya tuvieron respuesta; los cinco conductores con más viajes realizados.</summary>
public sealed record IndicadoresConductores(
    int ConductoresConAsignaciones,
    int Asignaciones,
    decimal? PorcentajeAceptacion,
    decimal? PorcentajeRechazo,
    decimal? PorcentajeVencidas,
    decimal? CalificacionPromedio,
    IReadOnlyList<ConductorReporteLectura> MasViajes);

/// <summary>Un exporte listo: enlace firmado de solo lectura que vence en <see cref="ExpiraEn"/> (ADR-0013).</summary>
public sealed record ExporteReporte(Guid Id, TipoReporte Tipo, string NombreArchivo, int Filas, Uri Url, DateTimeOffset ExpiraEn);
