using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Reportes.Entrada.Dto;
using FlowRide.Application.Reportes.Entrada.Puertos;
using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Reportes;
using FlowRide.Domain.Reportes.GestionReportes;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Reportes.Entrada.Adaptadores;

public sealed class ReportesAdaptadorEntrada(
    ICasoUsoConRetorno<VistaCentralizadaDominio, VistaCentralizadaLectura> vistaCentralizada,
    ICasoUsoConRetorno<ConsultarIndicadoresDominio, IndicadoresLectura> indicadores,
    ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenViajes, ViajeReporteLectura>> viajes,
    ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenSolicitudes, SolicitudReporteFila>> solicitudes,
    ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenConductores, ConductorReporteLectura>> conductores,
    ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenEmpresas, EmpresaReporteLectura>> empresas,
    ICasoUsoConRetorno<ExportarReporteDominio, ExporteReporte> exportar) : IReportesPuertoEntrada
{
    public Task<VistaCentralizadaLectura> VistaCentralizadaAsync(VistaCentralizadaPeticion peticion, string rol, CancellationToken ct) =>
        vistaCentralizada.EjecutarAsync(new VistaCentralizadaDominio(peticion?.Fecha, peticion?.EmpresaClienteId, Lector(rol)), ct);

    public Task<IndicadoresLectura> IndicadoresAsync(ReportePeticion peticion, CancellationToken ct) =>
        indicadores.EjecutarAsync(new ConsultarIndicadoresDominio(Periodo(peticion)), ct);

    public async Task<ReporteRespuesta<ResumenViajes, ViajeReporteLectura>> ViajesAsync(ReportePeticion peticion, string rol,
        CancellationToken ct) =>
        Respuesta(await viajes.EjecutarAsync(Consulta(peticion, rol), ct));

    public async Task<ReporteRespuesta<ResumenSolicitudes, SolicitudReporteFila>> SolicitudesAsync(ReportePeticion peticion, string rol,
        CancellationToken ct) =>
        Respuesta(await solicitudes.EjecutarAsync(Consulta(peticion, rol), ct));

    public async Task<ReporteRespuesta<ResumenConductores, ConductorReporteLectura>> ConductoresAsync(ReportePeticion peticion,
        CancellationToken ct) =>
        Respuesta(await conductores.EjecutarAsync(Consulta(peticion, RolesPanel.Administrador), ct));

    public async Task<ReporteRespuesta<ResumenEmpresas, EmpresaReporteLectura>> EmpresasAsync(ReportePeticion peticion, CancellationToken ct) =>
        Respuesta(await empresas.EjecutarAsync(Consulta(peticion, RolesPanel.Administrador), ct));

    public async Task<ExporteRespuesta> ExportarAsync(string tipo, ReportePeticion peticion, string rol, Guid? actorId, CancellationToken ct)
    {
        var tipoReporte = Enumeracion<TipoReporte>(tipo, "tipo")
                          ?? throw new FlowRideExcepcion($"tipo debe ser uno de: {string.Join(", ", Enum.GetNames<TipoReporte>())}.");
        var actor = actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.");
        var exporte = await exportar.EjecutarAsync(new ExportarReporteDominio(tipoReporte, Filtro(peticion), Lector(rol), actor), ct);
        return new ExporteRespuesta(exporte.Id, exporte.Tipo.ToString(), exporte.NombreArchivo, exporte.Filas, exporte.Url, exporte.ExpiraEn);
    }

    private static ConsultarReporteDominio Consulta(ReportePeticion peticion, string rol) =>
        new(Filtro(peticion), new Paginacion(peticion?.Pagina, peticion?.TamanoPagina), Lector(rol));

    private static FiltroReporte Filtro(ReportePeticion peticion) =>
        new(Periodo(peticion), peticion?.ConductorId, peticion?.EmpresaClienteId, Enumeracion<GrupoSolicitud>(peticion?.Grupo, "grupo"));

    private static PeriodoReporte Periodo(ReportePeticion? peticion) =>
        peticion?.Desde is { } desde && peticion.Hasta is { } hasta
            ? new PeriodoReporte(desde, hasta)
            : throw new FlowRideExcepcion("desde y hasta son obligatorios.");

    private static ReporteRespuesta<TResumen, TFila> Respuesta<TResumen, TFila>(ReporteLectura<TResumen, TFila> reporte) =>
        new(reporte.Desde, reporte.Hasta, reporte.Resumen, new PaginaRespuesta<TFila>(reporte.Filas.Elementos, reporte.Filas.Total,
            reporte.Filas.Paginacion.Pagina, reporte.Filas.Paginacion.Tamano));

    private static TEnum? Enumeracion<TEnum>(string? valor, string campo) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        return Enum.TryParse<TEnum>(valor.Trim(), ignoreCase: true, out var resultado) && Enum.IsDefined(resultado)
            ? resultado
            : throw new FlowRideExcepcion($"{campo} debe ser uno de: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static LectorSolicitud Lector(string rol) =>
        rol == RolesPanel.OperadorLogistico ? LectorSolicitud.OperadorLogistico : LectorSolicitud.Administrador;
}
