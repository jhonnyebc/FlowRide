using FlowRide.Application.Reportes.Entrada.Dto;
using FlowRide.Application.Reportes.Salida.Puertos;

namespace FlowRide.Application.Reportes.Entrada.Puertos;

// rol: rol del Panel Web, decide la visibilidad de las solicitudes personales. actorId: usuario interno (auditoría).

public interface IReportesPuertoEntrada
{
    Task<VistaCentralizadaLectura> VistaCentralizadaAsync(VistaCentralizadaPeticion peticion, string rol, CancellationToken ct);

    Task<IndicadoresLectura> IndicadoresAsync(ReportePeticion peticion, CancellationToken ct);

    Task<ReporteRespuesta<ResumenViajes, ViajeReporteLectura>> ViajesAsync(ReportePeticion peticion, string rol, CancellationToken ct);

    Task<ReporteRespuesta<ResumenSolicitudes, SolicitudReporteFila>> SolicitudesAsync(ReportePeticion peticion, string rol, CancellationToken ct);

    Task<ReporteRespuesta<ResumenConductores, ConductorReporteLectura>> ConductoresAsync(ReportePeticion peticion, CancellationToken ct);

    Task<ReporteRespuesta<ResumenEmpresas, EmpresaReporteLectura>> EmpresasAsync(ReportePeticion peticion, CancellationToken ct);

    /// <summary>Tipo: viajes, solicitudes, conductores o empresas.</summary>
    Task<ExporteRespuesta> ExportarAsync(string tipo, ReportePeticion peticion, string rol, Guid? actorId, CancellationToken ct);
}
