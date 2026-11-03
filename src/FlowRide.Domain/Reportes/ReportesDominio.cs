using System.Text.Json.Serialization;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Reportes;

/// <summary>Reportes operacionales de V1 (RF-46 a RF-49).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoReporte
{
    Viajes,
    Solicitudes,
    Conductores,
    Empresas
}

/// <summary>Cómo cuenta una solicitud en el reporte de atendidas y pendientes (RF-47).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GrupoSolicitud
{
    Atendida,
    Pendiente,
    Cancelada
}

/// <summary>Semaforización de la vista centralizada (RF-36, OPLOG-UX-02).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NivelAtencion
{
    Critico,
    Atencion,
    Normal
}

/// <summary>
/// Periodo de un reporte: desde (incluido) y hasta (excluido) la hora programada del servicio. Se limita a
/// <see cref="DiasMaximos"/> días para que el reporte responda dentro del objetivo de ADM-REND-01 (P95 &lt; 2 s).
/// </summary>
public sealed record PeriodoReporte
{
    public const int DiasMaximos = 93;

    public PeriodoReporte(DateTimeOffset desde, DateTimeOffset hasta)
    {
        if (hasta <= desde)
            throw new FlowRideExcepcion("hasta debe ser posterior a desde.");
        if (hasta - desde > TimeSpan.FromDays(DiasMaximos))
            throw new FlowRideExcepcion($"El periodo de un reporte es de máximo {DiasMaximos} días.");
        Desde = desde;
        Hasta = hasta;
    }

    public DateTimeOffset Desde { get; }
    public DateTimeOffset Hasta { get; }

    /// <summary>El día completo en la zona horaria del operador.</summary>
    public static PeriodoReporte DelDia(DateOnly fecha, ZonaHorariaOperador zona)
    {
        ArgumentNullException.ThrowIfNull(zona);
        return new PeriodoReporte(zona.Instante(fecha, TimeOnly.MinValue), zona.Instante(fecha.AddDays(1), TimeOnly.MinValue));
    }

    /// <summary>Un periodo que ya terminó no recibe servicios nuevos: sus indicadores admiten caché (ADR-0006).</summary>
    public bool CerradoEn(DateTimeOffset ahora) => Hasta <= ahora;
}

/// <summary>Reglas de cálculo de los indicadores y reportes (RF-36, RF-42, RF-46 a RF-49).</summary>
public static class ReglasReportes
{
    /// <summary>Filas máximas que lee un reporte; si el periodo tiene más, se pide acotarlo.</summary>
    public const int FilasMaximas = 50_000;

    /// <summary>Un servicio sin conductor confirmado a menos de este margen de su hora pasa a crítico.</summary>
    public static readonly TimeSpan MargenCritico = TimeSpan.FromMinutes(30);

    public static void ValidarFilas(int filas)
    {
        if (filas > FilasMaximas)
            throw new FlowRideExcepcion($"El periodo tiene más de {FilasMaximas} registros: acota el rango de fechas o filtra el reporte.");
    }

    /// <summary>Atendida si el servicio se prestó o se está prestando; pendiente mientras espera conductor o inicio.</summary>
    public static GrupoSolicitud Grupo(EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Completada or EstadoSolicitud.EnCurso => GrupoSolicitud.Atendida,
        EstadoSolicitud.Cancelada => GrupoSolicitud.Cancelada,
        _ => GrupoSolicitud.Pendiente
    };

    /// <summary>Puntual si el viaje empezó a más tardar <paramref name="tolerancia"/> después de la hora programada.</summary>
    public static bool Puntual(DateTimeOffset programado, DateTimeOffset? inicioReal, TimeSpan tolerancia) =>
        inicioReal is { } real && real <= programado + tolerancia;

    public static int MinutosRetraso(DateTimeOffset programado, DateTimeOffset? inicioReal) =>
        inicioReal is { } real && real > programado ? (int)(real - programado).TotalMinutes : 0;

    /// <summary>Porcentaje con un decimal; null si no hay base (0 de 0 no es 0 %).</summary>
    public static decimal? Porcentaje(int parte, int total) =>
        total == 0 ? null : Math.Round(100m * parte / total, 1, MidpointRounding.AwayFromZero);

    public static decimal? Promedio(IEnumerable<int> valores)
    {
        var lista = valores.ToList();
        return lista.Count == 0 ? null : Math.Round((decimal)lista.Sum() / lista.Count, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Nivel de atención de un servicio en la vista centralizada (OPLOG-UX-02). Crítico: sin candidatos, viaje programado
    /// cuya hora ya pasó, o sin conductor confirmado a menos de <see cref="MargenCritico"/>. Atención: espera conductor o
    /// su respuesta, es urgente o tiene incidentes. Normal: el resto, incluidos los cerrados.
    /// </summary>
    public static (NivelAtencion Nivel, string? Motivo) Nivel(ServicioEnCurso servicio, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(servicio);
        if (servicio.Estado is EstadoSolicitud.Completada or EstadoSolicitud.Cancelada)
            return (NivelAtencion.Normal, null);
        if (servicio.Estado == EstadoSolicitud.SinCandidatos)
            return (NivelAtencion.Critico, "Sin conductor disponible.");
        if (servicio.EstadoViaje == EstadoViaje.Programado && servicio.FechaHoraServicio < ahora)
            return (NivelAtencion.Critico, $"Retrasado {(int)(ahora - servicio.FechaHoraServicio).TotalMinutes} min.");

        var sinConfirmar = servicio.Estado == EstadoSolicitud.Pendiente || servicio.EstadoAsignacion == EstadoAsignacion.Asignada;
        if (sinConfirmar && servicio.FechaHoraServicio - ahora <= MargenCritico)
            return (NivelAtencion.Critico, servicio.Estado == EstadoSolicitud.Pendiente
                ? "Sin conductor y el servicio está por empezar."
                : "El conductor no ha confirmado y el servicio está por empezar.");

        if (servicio.Estado == EstadoSolicitud.Pendiente)
            return (NivelAtencion.Atencion, "Espera asignación.");
        if (servicio.EstadoAsignacion == EstadoAsignacion.Asignada)
            return (NivelAtencion.Atencion, "Espera la respuesta del conductor.");
        if (servicio.Incidentes > 0)
            return (NivelAtencion.Atencion, $"{servicio.Incidentes} incidente(s) reportado(s).");
        if (servicio.Urgente)
            return (NivelAtencion.Atencion, "Servicio urgente.");
        return (NivelAtencion.Normal, null);
    }
}

/// <summary>Lo que la semaforización necesita saber de un servicio.</summary>
public sealed record ServicioEnCurso(
    EstadoSolicitud Estado,
    EstadoAsignacion? EstadoAsignacion,
    EstadoViaje? EstadoViaje,
    DateTimeOffset FechaHoraServicio,
    bool Urgente,
    int Incidentes);
