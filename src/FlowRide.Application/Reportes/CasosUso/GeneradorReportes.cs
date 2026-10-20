using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Reportes;
using FlowRide.Domain.Reportes.GestionReportes;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.Application.Reportes.CasosUso;

/// <summary>Un reporte con todas sus filas: la consulta lo pagina y el exporte lo escribe completo.</summary>
public sealed record ReporteCompleto<TResumen, TFila>(PeriodoReporte Periodo, TResumen Resumen, IReadOnlyList<TFila> Filas)
{
    public ReporteLectura<TResumen, TFila> Pagina(Paginacion paginacion)
    {
        ArgumentNullException.ThrowIfNull(paginacion);
        return new ReporteLectura<TResumen, TFila>(Periodo.Desde, Periodo.Hasta, Resumen,
            new PaginaResultado<TFila>(Filas.Skip(paginacion.Salto).Take(paginacion.Tamano).ToList(), Filas.Count, paginacion));
    }
}

/// <summary>
/// Arma los reportes RF-46 a RF-49 y los indicadores RF-42 a partir de las filas del periodo: lee de PostgreSQL, aplica la
/// visibilidad de las solicitudes personales (PAS-SEG-01) y agrega en memoria con <see cref="ReglasReportes"/>.
/// </summary>
public sealed class GeneradorReportes(IConsultasReportes consultas, IConsultasViajes viajes, ParametrosDominio parametros)
{
    private const int Lectura = ReglasReportes.FilasMaximas + 1;
    private const int TopConductores = 5;

    /// <summary>Tolerancia de puntualidad (parámetro <c>reporte.tolerancia_puntualidad_minutos</c>).</summary>
    public async Task<int> ToleranciaMinutosAsync(CancellationToken ct) =>
        await parametros.EnteroAsync(CatalogoParametros.ToleranciaPuntualidadMinutos, ct);

    /// <summary>RF-46: viajes finalizados del periodo, con su puntualidad.</summary>
    public async Task<ReporteCompleto<ResumenViajes, ViajeReporteLectura>> ViajesAsync(FiltroReporte filtro, LectorSolicitud lector,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var tolerancia = await ToleranciaMinutosAsync(ct);
        var visibilidad = await VisibilidadAsync(ct);
        var realizados = await ViajesDelPeriodoAsync(filtro, EstadoViaje.Finalizado, ct);

        var filas = realizados.Select(v => new ViajeReporteLectura(
                VisibilidadSolicitudes.PuedeVerDetalle(v.EsPersonal, lector, visibilidad) ? v : v.SinDirecciones(),
                ReglasReportes.Puntual(v.InicioProgramado, v.FechaHoraInicio, TimeSpan.FromMinutes(tolerancia)),
                ReglasReportes.MinutosRetraso(v.InicioProgramado, v.FechaHoraInicio)))
            .ToList();
        var puntuales = filas.Count(f => f.Puntual);
        var calificados = realizados.Where(v => v.Puntaje is not null).Select(v => v.Puntaje!.Value).ToList();
        var resumen = new ResumenViajes(filas.Count, puntuales, ReglasReportes.Porcentaje(puntuales, filas.Count),
            ReglasReportes.Promedio(filas.Select(f => f.MinutosRetraso)), realizados.Sum(v => v.Incidentes), calificados.Count,
            ReglasReportes.Promedio(calificados), tolerancia);
        return new ReporteCompleto<ResumenViajes, ViajeReporteLectura>(filtro.Periodo, resumen, filas);
    }

    /// <summary>RF-47: solicitudes del periodo por grupo; el resumen cuenta todos los grupos aunque se filtre uno.</summary>
    public async Task<ReporteCompleto<ResumenSolicitudes, SolicitudReporteFila>> SolicitudesAsync(FiltroReporte filtro, LectorSolicitud lector,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var solicitudes = await SolicitudesVisiblesAsync(filtro, lector, ct);
        var filas = solicitudes.Select(s => new SolicitudReporteFila(s, ReglasReportes.Grupo(s.Estado))).ToList();

        var atendidas = filas.Count(f => f.Grupo == GrupoSolicitud.Atendida);
        var canceladas = filas.Count(f => f.Grupo == GrupoSolicitud.Cancelada);
        var resumen = new ResumenSolicitudes(filas.Count, atendidas, filas.Count - atendidas - canceladas, canceladas,
            ReglasReportes.Porcentaje(atendidas, filas.Count - canceladas), PorEstado(solicitudes));
        var visibles = filtro.Grupo is { } grupo ? filas.Where(f => f.Grupo == grupo).ToList() : filas;
        return new ReporteCompleto<ResumenSolicitudes, SolicitudReporteFila>(filtro.Periodo, resumen, visibles);
    }

    /// <summary>RF-48: conductores con asignaciones en el periodo, del que más viajes realizó al que menos.</summary>
    public async Task<ReporteCompleto<ResumenConductores, ConductorReporteLectura>> ConductoresAsync(FiltroReporte filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var tolerancia = await ToleranciaMinutosAsync(ct);
        var asignaciones = await consultas.AsignacionesAsync(filtro.Periodo, filtro.ConductorId, Lectura, ct);
        ReglasReportes.ValidarFilas(asignaciones.Count);

        var filas = PorConductor(asignaciones, TimeSpan.FromMinutes(tolerancia));
        var resumen = new ResumenConductores(filas.Count, asignaciones.Count, filas.Sum(f => f.Aceptadas), filas.Sum(f => f.Rechazadas),
            filas.Sum(f => f.Vencidas), filas.Sum(f => f.ViajesRealizados));
        return new ReporteCompleto<ResumenConductores, ConductorReporteLectura>(filtro.Periodo, resumen, filas);
    }

    /// <summary>RF-49: histórico del periodo por cliente empresarial (o de uno solo, si se filtra).</summary>
    public async Task<ReporteCompleto<ResumenEmpresas, EmpresaReporteLectura>> EmpresasAsync(FiltroReporte filtro, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var solicitudes = await consultas.SolicitudesAsync(filtro.Periodo, filtro.EmpresaClienteId, Lectura, ct);
        ReglasReportes.ValidarFilas(solicitudes.Count);

        var filas = PorEmpresa(solicitudes);
        var resumen = new ResumenEmpresas(filas.Count(f => f.EmpresaClienteId is not null), solicitudes.Count, filas.Sum(f => f.ViajesRealizados));
        return new ReporteCompleto<ResumenEmpresas, EmpresaReporteLectura>(filtro.Periodo, resumen, filas);
    }

    /// <summary>RF-42: volumen, cumplimiento, conductores y clientes empresariales del periodo.</summary>
    public async Task<IndicadoresLectura> IndicadoresAsync(PeriodoReporte periodo, int toleranciaMinutos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        var filtro = new FiltroReporte(periodo);
        var solicitudes = await consultas.SolicitudesAsync(periodo, null, Lectura, ct);
        var todosViajes = await ViajesDelPeriodoAsync(filtro, null, ct);
        var asignaciones = await consultas.AsignacionesAsync(periodo, null, Lectura, ct);
        ReglasReportes.ValidarFilas(Math.Max(solicitudes.Count, asignaciones.Count));
        var tolerancia = TimeSpan.FromMinutes(toleranciaMinutos);

        var grupos = solicitudes.Select(s => ReglasReportes.Grupo(s.Estado)).ToList();
        var volumen = new IndicadoresVolumen(solicitudes.Count, grupos.Count(g => g == GrupoSolicitud.Atendida),
            grupos.Count(g => g == GrupoSolicitud.Pendiente), grupos.Count(g => g == GrupoSolicitud.Cancelada), solicitudes.Count(s => s.Urgente),
            PorEstado(solicitudes), todosViajes.Count(v => v.Estado == EstadoViaje.Finalizado),
            todosViajes.Count(v => v.Estado == EstadoViaje.Cancelado));

        var iniciados = todosViajes.Where(v => v.FechaHoraInicio is not null).ToList();
        var puntuales = iniciados.Count(v => ReglasReportes.Puntual(v.InicioProgramado, v.FechaHoraInicio, tolerancia));
        var cumplimiento = new IndicadoresCumplimiento(iniciados.Count, puntuales, ReglasReportes.Porcentaje(puntuales, iniciados.Count),
            ReglasReportes.Promedio(iniciados.Select(v => ReglasReportes.MinutosRetraso(v.InicioProgramado, v.FechaHoraInicio))),
            todosViajes.Sum(v => v.Incidentes), toleranciaMinutos);

        var conductores = PorConductor(asignaciones, tolerancia);
        var (aceptadas, rechazadas, vencidas) = (conductores.Sum(c => c.Aceptadas), conductores.Sum(c => c.Rechazadas), conductores.Sum(c => c.Vencidas));
        var respondidas = aceptadas + rechazadas + vencidas;
        var indicadoresConductores = new IndicadoresConductores(conductores.Count, asignaciones.Count,
            ReglasReportes.Porcentaje(aceptadas, respondidas), ReglasReportes.Porcentaje(rechazadas, respondidas),
            ReglasReportes.Porcentaje(vencidas, respondidas),
            ReglasReportes.Promedio(todosViajes.Where(v => v.Puntaje is not null).Select(v => v.Puntaje!.Value)),
            conductores.Where(c => c.ViajesRealizados > 0).Take(TopConductores).ToList());

        return new IndicadoresLectura(periodo.Desde, periodo.Hasta, volumen, cumplimiento, indicadoresConductores, PorEmpresa(solicitudes));
    }

    private async Task<IReadOnlyList<ViajeLectura>> ViajesDelPeriodoAsync(FiltroReporte filtro, EstadoViaje? estado, CancellationToken ct)
    {
        var lista = await viajes.ListarAsync(new FiltroViajes(estado, filtro.ConductorId, null, filtro.EmpresaClienteId, filtro.Periodo.Desde,
            filtro.Periodo.Hasta), Lectura, ct);
        ReglasReportes.ValidarFilas(lista.Count);
        return lista;
    }

    /// <summary>Solicitudes del periodo sin las direcciones que el lector no puede ver (también para la vista centralizada).</summary>
    public async Task<IReadOnlyList<SolicitudReporteLectura>> SolicitudesVisiblesAsync(FiltroReporte filtro, LectorSolicitud lector,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        var solicitudes = await consultas.SolicitudesAsync(filtro.Periodo, filtro.EmpresaClienteId, Lectura, ct);
        ReglasReportes.ValidarFilas(solicitudes.Count);
        var visibilidad = await VisibilidadAsync(ct);
        return solicitudes.Select(s => VisibilidadSolicitudes.PuedeVerDetalle(s.EsPersonal, lector, visibilidad) ? s : s.SinDirecciones())
            .ToList();
    }

    private Task<string> VisibilidadAsync(CancellationToken ct) =>
        parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);

    private static Dictionary<string, int> PorEstado(IReadOnlyList<SolicitudReporteLectura> solicitudes) =>
        Enum.GetValues<EstadoSolicitud>().ToDictionary(estado => estado.ToString(), estado => solicitudes.Count(s => s.Estado == estado),
            StringComparer.Ordinal);

    private static List<ConductorReporteLectura> PorConductor(IReadOnlyList<AsignacionReporteLectura> asignaciones, TimeSpan tolerancia) =>
        asignaciones.GroupBy(a => a.ConductorId)
            .Select(grupo =>
            {
                var lista = grupo.ToList();
                var aceptadas = lista.Count(a => a.ViajeId is not null);
                var vencidas = lista.Count(a => a.Vencida);
                var rechazadas = lista.Count(a => a.Estado == EstadoAsignacion.Rechazada && !a.Vencida);
                var realizados = lista.Where(a => a.EstadoViaje == EstadoViaje.Finalizado).ToList();
                var puntuales = realizados.Count(a => ReglasReportes.Puntual(a.InicioProgramado, a.InicioReal, tolerancia));
                return new ConductorReporteLectura(grupo.Key, lista[0].ConductorNombre,
                    string.Join(", ", lista.Select(a => a.Placa).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                    lista.Count, aceptadas, rechazadas, vencidas, lista.Count(a => a.Estado == EstadoAsignacion.Asignada),
                    ReglasReportes.Porcentaje(aceptadas, aceptadas + rechazadas + vencidas), realizados.Count, puntuales,
                    ReglasReportes.Porcentaje(puntuales, realizados.Count), lista.Sum(a => a.Incidentes),
                    ReglasReportes.Promedio(lista.Where(a => a.Puntaje is not null).Select(a => a.Puntaje!.Value)));
            })
            .OrderByDescending(c => c.ViajesRealizados).ThenBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.ConductorId)
            .ToList();

    private static List<EmpresaReporteLectura> PorEmpresa(IReadOnlyList<SolicitudReporteLectura> solicitudes) =>
        solicitudes.GroupBy(s => s.EmpresaClienteId)
            .Select(grupo =>
            {
                var lista = grupo.ToList();
                var grupos = lista.Select(s => ReglasReportes.Grupo(s.Estado)).ToList();
                return new EmpresaReporteLectura(grupo.Key, lista[0].EmpresaClienteNombre ?? "Sin empresa", lista.Count,
                    grupos.Count(g => g == GrupoSolicitud.Atendida), grupos.Count(g => g == GrupoSolicitud.Pendiente),
                    grupos.Count(g => g == GrupoSolicitud.Cancelada), lista.Count(s => s.Urgente), lista.Select(s => s.PasajeroId).Distinct().Count(),
                    lista.Count(s => s.EstadoViaje == EstadoViaje.Finalizado), lista.Sum(s => s.Incidentes),
                    ReglasReportes.Promedio(lista.Where(s => s.Puntaje is not null).Select(s => s.Puntaje!.Value)));
            })
            .OrderByDescending(e => e.Solicitudes).ThenBy(e => e.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
