using System.Globalization;
using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Operacion.Salida.Puertos;
using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Reportes;
using FlowRide.Domain.Reportes.GestionReportes;

namespace FlowRide.Application.Reportes.CasosUso;

/// <summary>
/// Vista centralizada de solicitudes y viajes del día (RF-36, OPLOG-UX-02): cada servicio con su nivel de atención,
/// primero los críticos. Lee siempre de PostgreSQL: es estado operacional (ADR-0006 no permite caché aquí).
/// </summary>
public sealed class VistaCentralizadaCasoUso(GeneradorReportes generador, ZonaHorariaOperador zona, TimeProvider reloj)
    : ICasoUsoConRetorno<VistaCentralizadaDominio, VistaCentralizadaLectura>
{
    public async Task<VistaCentralizadaLectura> EjecutarAsync(VistaCentralizadaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var periodo = PeriodoReporte.DelDia(datos.Fecha ?? DateOnly.FromDateTime(zona.Local(ahora).DateTime), zona);
        var solicitudes = await generador.SolicitudesVisiblesAsync(new FiltroReporte(periodo, EmpresaClienteId: datos.EmpresaClienteId),
            datos.Lector, ct);

        var servicios = solicitudes
            .Select(s =>
            {
                var (nivel, motivo) = ReglasReportes.Nivel(
                    new ServicioEnCurso(s.Estado, s.EstadoAsignacion, s.EstadoViaje, s.FechaHoraServicio, s.Urgente, s.Incidentes), ahora);
                return new ServicioVistaLectura(s, nivel, motivo);
            })
            .OrderBy(s => s.Nivel).ThenBy(s => s.Servicio.FechaHoraServicio).ThenBy(s => s.Servicio.Id)
            .ToList();
        return new VistaCentralizadaLectura(periodo.Desde, periodo.Hasta, servicios.Count(s => s.Nivel == NivelAtencion.Critico),
            servicios.Count(s => s.Nivel == NivelAtencion.Atencion), servicios.Count(s => s.Nivel == NivelAtencion.Normal), servicios);
    }
}

/// <summary>
/// Indicadores básicos (RF-42). Un periodo cerrado se sirve desde la caché (ADR-0006, TTL acotado); el que sigue abierto
/// siempre se calcula contra PostgreSQL.
/// </summary>
public sealed class ConsultarIndicadoresCasoUso(GeneradorReportes generador, ICacheReportes cache, TimeProvider reloj)
    : ICasoUsoConRetorno<ConsultarIndicadoresDominio, IndicadoresLectura>
{
    public async Task<IndicadoresLectura> EjecutarAsync(ConsultarIndicadoresDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var periodo = datos.Periodo;
        var tolerancia = await generador.ToleranciaMinutosAsync(ct);
        if (!periodo.CerradoEn(reloj.GetUtcNow()))
            return await generador.IndicadoresAsync(periodo, tolerancia, ct);

        var clave = string.Create(CultureInfo.InvariantCulture,
            $"reportes:indicadores:{periodo.Desde.UtcTicks}:{periodo.Hasta.UtcTicks}:{tolerancia}");
        return await cache.ObtenerOCrearAsync(clave, token => generador.IndicadoresAsync(periodo, tolerancia, token), ct);
    }
}

/// <summary>Reporte de viajes realizados (RF-46).</summary>
public sealed class ReporteViajesCasoUso(GeneradorReportes generador)
    : ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenViajes, ViajeReporteLectura>>
{
    public async Task<ReporteLectura<ResumenViajes, ViajeReporteLectura>> EjecutarAsync(ConsultarReporteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return (await generador.ViajesAsync(datos.Filtro, datos.Lector, ct)).Pagina(datos.Paginacion);
    }
}

/// <summary>Reporte de solicitudes atendidas y pendientes (RF-47).</summary>
public sealed class ReporteSolicitudesCasoUso(GeneradorReportes generador)
    : ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenSolicitudes, SolicitudReporteFila>>
{
    public async Task<ReporteLectura<ResumenSolicitudes, SolicitudReporteFila>> EjecutarAsync(ConsultarReporteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return (await generador.SolicitudesAsync(datos.Filtro, datos.Lector, ct)).Pagina(datos.Paginacion);
    }
}

/// <summary>Reporte de conductores asignados (RF-48).</summary>
public sealed class ReporteConductoresCasoUso(GeneradorReportes generador)
    : ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenConductores, ConductorReporteLectura>>
{
    public async Task<ReporteLectura<ResumenConductores, ConductorReporteLectura>> EjecutarAsync(ConsultarReporteDominio datos,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return (await generador.ConductoresAsync(datos.Filtro, ct)).Pagina(datos.Paginacion);
    }
}

/// <summary>Reporte histórico por cliente empresarial (RF-49).</summary>
public sealed class ReporteEmpresasCasoUso(GeneradorReportes generador)
    : ICasoUsoConRetorno<ConsultarReporteDominio, ReporteLectura<ResumenEmpresas, EmpresaReporteLectura>>
{
    public async Task<ReporteLectura<ResumenEmpresas, EmpresaReporteLectura>> EjecutarAsync(ConsultarReporteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return (await generador.EmpresasAsync(datos.Filtro, ct)).Pagina(datos.Paginacion);
    }
}

/// <summary>
/// Exporta un reporte completo a CSV (decisión del 2026-10-19: síncrono, dentro del periodo máximo) en el bucket del
/// operador bajo <c>exports/</c> (ADR-0013: borrado a los 7 días por política de ciclo de vida) y devuelve un enlace
/// firmado de 15 minutos. El exporte queda auditado; si la auditoría falla, el archivo se borra.
/// </summary>
public sealed class ExportarReporteCasoUso(
    GeneradorReportes generador,
    IAlmacenObjetos almacen,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    ZonaHorariaOperador zona,
    TimeProvider reloj) : ICasoUsoConRetorno<ExportarReporteDominio, ExporteReporte>
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);

    public async Task<ExporteReporte> EjecutarAsync(ExportarReporteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var archivo = await ArmarAsync(datos, ct);
        var ahora = reloj.GetUtcNow();
        var id = Guid.CreateVersion7(ahora);
        var periodo = datos.Filtro.Periodo;
        var nombre = string.Create(CultureInfo.InvariantCulture,
            $"reporte-{datos.Tipo.ToString().ToLowerInvariant()}-{Dia(periodo.Desde)}-{Dia(periodo.Hasta.AddTicks(-1))}.csv");
        var ruta = $"exports/{datos.Tipo.ToString().ToLowerInvariant()}/{id}/{nombre}";

        using (var contenido = new MemoryStream(archivo.Contenido(), writable: false))
            await almacen.GuardarAsync(ruta, contenido, ArchivoCsv.TipoContenido, ct);
        try
        {
            await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoReporte, AccionesAuditoria.Exportar, id, ahora,
                despues: Auditar.Valores(("tipo", datos.Tipo), ("desde", periodo.Desde), ("hasta", periodo.Hasta), ("filas", archivo.Filas),
                    ("conductor_id", datos.Filtro.ConductorId), ("empresa_cliente_id", datos.Filtro.EmpresaClienteId),
                    ("grupo", datos.Filtro.Grupo))), ct);
            await unidadTrabajo.GuardarCambiosAsync(ct);
        }
        catch
        {
            await almacen.EliminarAsync(ruta, CancellationToken.None);
            throw;
        }

        var url = await almacen.UrlDescargaAsync(ruta, Vigencia, ct);
        return new ExporteReporte(id, datos.Tipo, nombre, archivo.Filas, url, ahora.Add(Vigencia));
    }

    private string Dia(DateTimeOffset instante) => zona.Local(instante).ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    private async Task<ArchivoCsv> ArmarAsync(ExportarReporteDominio datos, CancellationToken ct)
    {
        switch (datos.Tipo)
        {
            case TipoReporte.Viajes:
                {
                    var csv = new ArchivoCsv(zona, "Viaje", "Solicitud", "Inicio programado", "Inicio real", "Fin real", "Puntual", "Minutos de retraso",
                        "Conductor", "Placa", "Pasajero", "Empresa", "Origen", "Destino", "Urgente", "Personal", "Incidentes", "Calificación");
                    foreach (var f in (await generador.ViajesAsync(datos.Filtro, datos.Lector, ct)).Filas)
                        csv.Fila(f.Viaje.Id, f.Viaje.SolicitudId, f.Viaje.InicioProgramado, f.Viaje.FechaHoraInicio, f.Viaje.FechaHoraFin, f.Puntual,
                            f.MinutosRetraso, f.Viaje.ConductorNombre, f.Viaje.Placa, f.Viaje.PasajeroNombre, f.Viaje.EmpresaClienteNombre,
                            f.Viaje.DireccionOrigen, f.Viaje.DireccionDestino, f.Viaje.Urgente, f.Viaje.EsPersonal, f.Viaje.Incidentes, f.Viaje.Puntaje);
                    return csv;
                }
            case TipoReporte.Solicitudes:
                {
                    var csv = new ArchivoCsv(zona, "Solicitud", "Grupo", "Estado", "Servicio", "Registrada", "Canal", "Pasajero", "Empresa", "Origen",
                        "Destino", "Pasajeros", "Urgente", "Personal", "Conductor", "Placa", "Estado del viaje", "Inicio real", "Fin real");
                    foreach (var f in (await generador.SolicitudesAsync(datos.Filtro, datos.Lector, ct)).Filas)
                    {
                        var s = f.Solicitud;
                        csv.Fila(s.Id, f.Grupo, s.Estado, s.FechaHoraServicio, s.RegistradaEn, s.Canal, s.PasajeroNombre, s.EmpresaClienteNombre,
                            s.DireccionOrigen, s.DireccionDestino, s.CantidadPasajeros, s.Urgente, s.EsPersonal, s.ConductorNombre, s.Placa,
                            s.EstadoViaje, s.InicioReal, s.FinReal);
                    }
                    return csv;
                }
            case TipoReporte.Conductores:
                {
                    var csv = new ArchivoCsv(zona, "Conductor", "Nombre", "Placas", "Asignaciones", "Aceptadas", "Rechazadas", "Vencidas",
                        "Sin respuesta", "% aceptación", "Viajes realizados", "Puntuales", "% puntualidad", "Incidentes", "Calificación promedio");
                    foreach (var c in (await generador.ConductoresAsync(datos.Filtro, ct)).Filas)
                        csv.Fila(c.ConductorId, c.Nombre, c.Placas, c.Asignaciones, c.Aceptadas, c.Rechazadas, c.Vencidas, c.SinRespuesta,
                            c.PorcentajeAceptacion, c.ViajesRealizados, c.Puntuales, c.PorcentajePuntualidad, c.Incidentes, c.CalificacionPromedio);
                    return csv;
                }
            default:
                {
                    var csv = new ArchivoCsv(zona, "Empresa", "Nombre", "Solicitudes", "Atendidas", "Pendientes", "Canceladas", "Urgentes", "Pasajeros",
                        "Viajes realizados", "Incidentes", "Calificación promedio");
                    foreach (var e in (await generador.EmpresasAsync(datos.Filtro, ct)).Filas)
                        csv.Fila(e.EmpresaClienteId, e.Nombre, e.Solicitudes, e.Atendidas, e.Pendientes, e.Canceladas, e.Urgentes, e.Pasajeros,
                            e.ViajesRealizados, e.Incidentes, e.CalificacionPromedio);
                    return csv;
                }
        }
    }
}
