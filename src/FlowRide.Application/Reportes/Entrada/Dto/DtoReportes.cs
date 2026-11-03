using FlowRide.Application.Operacion.Entrada.Dto;

namespace FlowRide.Application.Reportes.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.

/// <summary>Vista centralizada (RF-36): un día en la zona horaria del operador; hoy si no se indica.</summary>
public sealed record VistaCentralizadaPeticion
{
    public DateOnly? Fecha { get; init; }
    public Guid? EmpresaClienteId { get; init; }
}

/// <summary>
/// Periodo y filtros de un reporte o exporte. Desde (incluido) y hasta (excluido) la hora programada del servicio, de
/// máximo 93 días. Cada reporte usa los filtros que le aplican.
/// </summary>
public sealed record ReportePeticion
{
    public DateTimeOffset? Desde { get; init; }
    public DateTimeOffset? Hasta { get; init; }
    public Guid? ConductorId { get; init; }
    public Guid? EmpresaClienteId { get; init; }

    /// <summary>Solo el reporte de solicitudes: Atendida, Pendiente o Cancelada.</summary>
    public string? Grupo { get; init; }

    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

/// <summary>Un reporte: periodo, resumen de todo el periodo y la página de filas.</summary>
public sealed record ReporteRespuesta<TResumen, TFila>(DateTimeOffset Desde, DateTimeOffset Hasta, TResumen Resumen, PaginaRespuesta<TFila> Filas);

/// <summary>Enlace firmado de descarga del CSV; vence en 15 minutos (ADR-0013).</summary>
public sealed record ExporteRespuesta(Guid Id, string Tipo, string NombreArchivo, int Filas, Uri Url, DateTimeOffset ExpiraEn);
