using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Solicitudes;

namespace FlowRide.Domain.Reportes.GestionReportes;

// Datos de entrada de los casos de uso de reportes (RF-36, RF-42, RF-46 a RF-49). El lector decide la visibilidad de
// las direcciones de las solicitudes personales (PAS-SEG-01).

/// <summary>Filtros comunes de los reportes; cada reporte usa los que le aplican.</summary>
public sealed record FiltroReporte(
    PeriodoReporte Periodo,
    Guid? ConductorId = null,
    Guid? EmpresaClienteId = null,
    GrupoSolicitud? Grupo = null);

/// <summary>Vista centralizada de un día (por defecto, hoy en la zona horaria del operador).</summary>
public sealed record VistaCentralizadaDominio(DateOnly? Fecha, Guid? EmpresaClienteId, LectorSolicitud Lector);

public sealed record ConsultarIndicadoresDominio(PeriodoReporte Periodo);

public sealed record ConsultarReporteDominio(FiltroReporte Filtro, Paginacion Paginacion, LectorSolicitud Lector);

public sealed record ExportarReporteDominio(TipoReporte Tipo, FiltroReporte Filtro, LectorSolicitud Lector, Guid ActorId);
