using System.Text;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Reportes.CasosUso;
using FlowRide.Application.Reportes.Entrada.Adaptadores;
using FlowRide.Application.Reportes.Entrada.Dto;
using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Reportes;
using FlowRide.Domain.Reportes.GestionReportes;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Identidad;
using FlowRide.UnitTests.Application.Operacion;
using FlowRide.UnitTests.Application.Tenancy;
using FlowRide.UnitTests.Application.Viajes;

namespace FlowRide.UnitTests.Application.Reportes;

internal sealed class ConsultasReportesEnMemoria : IConsultasReportes
{
    public List<SolicitudReporteLectura> Solicitudes { get; } = [];
    public List<AsignacionReporteLectura> Asignaciones { get; } = [];

    public Task<IReadOnlyList<SolicitudReporteLectura>> SolicitudesAsync(PeriodoReporte periodo, Guid? empresaClienteId, int maximo,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SolicitudReporteLectura>>(Solicitudes
            .Where(s => s.FechaHoraServicio >= periodo.Desde && s.FechaHoraServicio < periodo.Hasta)
            .Where(s => empresaClienteId is null || s.EmpresaClienteId == empresaClienteId)
            .Take(maximo).ToList());

    public Task<IReadOnlyList<AsignacionReporteLectura>> AsignacionesAsync(PeriodoReporte periodo, Guid? conductorId, int maximo,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AsignacionReporteLectura>>(Asignaciones
            .Where(a => a.InicioProgramado >= periodo.Desde && a.InicioProgramado < periodo.Hasta)
            .Where(a => conductorId is null || a.ConductorId == conductorId)
            .Take(maximo).ToList());
}

internal sealed class CacheReportesEnMemoria : ICacheReportes
{
    private readonly Dictionary<string, object> _valores = new(StringComparer.Ordinal);
    public int Creaciones { get; private set; }

    public async Task<T> ObtenerOCrearAsync<T>(string clave, Func<CancellationToken, Task<T>> crear, CancellationToken ct) where T : class
    {
        if (_valores.TryGetValue(clave, out var valor))
            return (T)valor;
        Creaciones++;
        var nuevo = await crear(ct);
        _valores[clave] = nuevo;
        return nuevo;
    }
}

public class ReportesCasosUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly PeriodoReporte Octubre = new(new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 1, 5, 0, 0, TimeSpan.Zero));
    private static readonly PeriodoReporte Septiembre = new(new DateTimeOffset(2026, 9, 1, 5, 0, 0, TimeSpan.Zero), Octubre.Desde);
    private static readonly ZonaHorariaOperador Bogota = new(TimeZoneInfo.CreateCustomTimeZone("Bogota", TimeSpan.FromHours(-5), "Bogotá", "Bogotá"));

    private readonly ConsultasReportesEnMemoria _consultas = new();
    private readonly ConsultasViajesEnMemoria _viajes = new();
    private readonly ParametrosEnMemoria _parametros = new();
    private readonly CacheReportesEnMemoria _cache = new();
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly AuditoriaEnMemoria _auditoria = new();
    private readonly UnidadTrabajoEnMemoria _unidadTrabajo;
    private readonly Guid _empresaId = Guid.NewGuid();

    public ReportesCasosUsoTests() => _unidadTrabajo = new UnidadTrabajoEnMemoria(_auditoria);

    private GeneradorReportes Generador => new(_consultas, _viajes, new ParametrosDominio(_parametros));
    private static readonly RelojFijo Reloj = new(Ahora);

    private static ViajeLectura Viaje(EstadoViaje estado, DateTimeOffset programado, int? retraso = null, int? puntaje = null, bool personal = false,
        Guid? conductorId = null, int incidentes = 0) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), estado, conductorId ?? Guid.NewGuid(), "Carlos", Guid.NewGuid(), "ABC123",
            Guid.NewGuid(), "=Ana", null, null, "Hotel; \"Central\"", "Aeropuerto", false, personal, programado, programado.AddMinutes(45),
            retraso is { } minutos ? programado.AddMinutes(minutos) : null, retraso is { } m ? programado.AddMinutes(m + 45) : null, incidentes,
            puntaje);

    private SolicitudReporteLectura Solicitud(EstadoSolicitud estado, DateTimeOffset servicio, bool conEmpresa = false, Guid? pasajeroId = null,
        EstadoAsignacion? asignacion = null, EstadoViaje? viaje = null, int? puntaje = null, bool personal = false, bool urgente = false) =>
        new(Guid.NewGuid(), pasajeroId ?? Guid.NewGuid(), "Ana", conEmpresa ? _empresaId : null, conEmpresa ? "Minera" : null, "Campamento",
            "Mina", servicio, 2, urgente, personal, CanalSolicitud.WhatsApp, estado, servicio.AddDays(-1), asignacion is null ? null : Guid.NewGuid(),
            asignacion, asignacion is null ? null : Guid.NewGuid(), asignacion is null ? null : "Carlos", asignacion is null ? null : "ABC123",
            viaje is null ? null : Guid.NewGuid(), viaje, null, null, 0, puntaje);

    private static AsignacionReporteLectura Asignacion(Guid conductorId, string nombre, EstadoAsignacion estado, bool vencida = false,
        EstadoViaje? viaje = null, int? retraso = null, int? puntaje = null, string placa = "ABC123") =>
        new(Guid.NewGuid(), Guid.NewGuid(), conductorId, nombre, placa, OrigenAsignacion.Automatica, estado, vencida, Ahora,
            viaje is null ? null : Guid.NewGuid(), viaje, retraso is { } m ? Ahora.AddMinutes(m) : null, 0, puntaje);

    [Fact]
    public async Task ReporteViajes_SoloLosRealizados_ConPuntualidadYCalificacion()
    {
        _viajes.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora, retraso: 5, puntaje: 5, personal: true));
        _viajes.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora.AddHours(1), retraso: 25, incidentes: 2));
        _viajes.Viajes.Add(Viaje(EstadoViaje.EnCurso, Ahora));

        var reporte = await new ReporteViajesCasoUso(Generador).EjecutarAsync(
            new ConsultarReporteDominio(new FiltroReporte(Octubre), new Paginacion(1, 1), LectorSolicitud.Administrador), CancellationToken.None);

        Assert.Equal(new ResumenViajes(2, 1, 50.0m, 15.0m, 2, 1, 5.0m, 10), reporte.Resumen);
        Assert.Equal((2, 1), (reporte.Filas.Total, reporte.Filas.Elementos.Count));
        var primera = reporte.Filas.Elementos[0];
        Assert.True(primera.Puntual);
        Assert.True(primera.Viaje.DetalleRestringido);
        Assert.Equal(EstadoViaje.Finalizado, _viajes.UltimoFiltro!.Estado);
        Assert.Equal((Octubre.Desde, Octubre.Hasta), (reporte.Desde, reporte.Hasta));
    }

    [Fact]
    public async Task ReporteSolicitudes_AgrupaAtendidasPendientesYCanceladas_YFiltraPorGrupo()
    {
        foreach (var estado in Enum.GetValues<EstadoSolicitud>())
            _consultas.Solicitudes.Add(Solicitud(estado, Ahora));
        var casoUso = new ReporteSolicitudesCasoUso(Generador);

        var pendientes = await casoUso.EjecutarAsync(new ConsultarReporteDominio(new FiltroReporte(Octubre, Grupo: GrupoSolicitud.Pendiente),
            Paginacion.PorDefecto, LectorSolicitud.OperadorLogistico), CancellationToken.None);

        var resumen = pendientes.Resumen;
        Assert.Equal((6, 2, 3, 1, (decimal?)40.0m), (resumen.Total, resumen.Atendidas, resumen.Pendientes, resumen.Canceladas, resumen.PorcentajeAtencion));
        Assert.All(Enum.GetNames<EstadoSolicitud>(), estado => Assert.Equal(1, resumen.PorEstado[estado]));
        Assert.Equal(3, pendientes.Filas.Total);
        Assert.All(pendientes.Filas.Elementos, f => Assert.Equal(GrupoSolicitud.Pendiente, f.Grupo));
    }

    [Fact]
    public async Task ReporteConductores_CuentaAceptadasRechazadasYVencidas()
    {
        var luis = Guid.NewGuid();
        var marta = Guid.NewGuid();
        _consultas.Asignaciones.Add(Asignacion(luis, "Luis", EstadoAsignacion.Finalizada, viaje: EstadoViaje.Finalizado, retraso: 3, puntaje: 4));
        _consultas.Asignaciones.Add(Asignacion(luis, "Luis", EstadoAsignacion.Rechazada, placa: "XYZ987"));
        _consultas.Asignaciones.Add(Asignacion(luis, "Luis", EstadoAsignacion.Rechazada, vencida: true));
        _consultas.Asignaciones.Add(Asignacion(marta, "Marta", EstadoAsignacion.Asignada));

        var reporte = await new ReporteConductoresCasoUso(Generador).EjecutarAsync(
            new ConsultarReporteDominio(new FiltroReporte(Octubre), Paginacion.PorDefecto, LectorSolicitud.Administrador), CancellationToken.None);

        Assert.Equal(new ResumenConductores(2, 4, 1, 1, 1, 1), reporte.Resumen);
        var (primero, segundo) = (reporte.Filas.Elementos[0], reporte.Filas.Elementos[1]);
        Assert.Equal(new ConductorReporteLectura(luis, "Luis", "ABC123, XYZ987", 3, 1, 1, 1, 0, 33.3m, 1, 1, 100.0m, 0, 4.0m), primero);
        Assert.Equal((marta, 1, (decimal?)null), (segundo.ConductorId, segundo.SinRespuesta, segundo.PorcentajeAceptacion));
    }

    [Fact]
    public async Task ReporteEmpresas_AgrupaPorCliente_YLosServiciosSinEmpresaAparte()
    {
        var pasajero = Guid.NewGuid();
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Ahora, conEmpresa: true, pasajeroId: pasajero,
            asignacion: EstadoAsignacion.Finalizada, viaje: EstadoViaje.Finalizado, puntaje: 3));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Cancelada, Ahora, conEmpresa: true, pasajeroId: pasajero, urgente: true));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Pendiente, Ahora, personal: true));

        var reporte = await new ReporteEmpresasCasoUso(Generador).EjecutarAsync(
            new ConsultarReporteDominio(new FiltroReporte(Octubre), Paginacion.PorDefecto, LectorSolicitud.Administrador), CancellationToken.None);

        Assert.Equal(new ResumenEmpresas(1, 3, 1), reporte.Resumen);
        Assert.Equal(new EmpresaReporteLectura(_empresaId, "Minera", 2, 1, 0, 1, 1, 1, 1, 0, 3.0m), reporte.Filas.Elementos[0]);
        Assert.Equal((null, "Sin empresa"), (reporte.Filas.Elementos[1].EmpresaClienteId, reporte.Filas.Elementos[1].Nombre));
    }

    [Fact]
    public async Task Indicadores_DelPeriodoAbierto_NoUsanCache_YLosDelCerradoSi()
    {
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Ahora, conEmpresa: true));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Pendiente, Ahora, urgente: true));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Septiembre.Desde.AddDays(2)));
        _viajes.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora, retraso: 20, puntaje: 4));
        _viajes.Viajes.Add(Viaje(EstadoViaje.Cancelado, Ahora));
        var conductor = Guid.NewGuid();
        _consultas.Asignaciones.Add(Asignacion(conductor, "Luis", EstadoAsignacion.Finalizada, viaje: EstadoViaje.Finalizado, retraso: 20));
        _consultas.Asignaciones.Add(Asignacion(conductor, "Luis", EstadoAsignacion.Rechazada, vencida: true));
        var casoUso = new ConsultarIndicadoresCasoUso(Generador, _cache, Reloj);

        var octubre = await casoUso.EjecutarAsync(new ConsultarIndicadoresDominio(Octubre), CancellationToken.None);
        var septiembre = await casoUso.EjecutarAsync(new ConsultarIndicadoresDominio(Septiembre), CancellationToken.None);
        var otraVez = await casoUso.EjecutarAsync(new ConsultarIndicadoresDominio(Septiembre), CancellationToken.None);

        Assert.Equal((2, 1, 1, 1), (octubre.Volumen.Solicitudes, octubre.Volumen.Atendidas, octubre.Volumen.Pendientes, octubre.Volumen.Urgentes));
        Assert.Equal((1, 1), (octubre.Volumen.ViajesRealizados, octubre.Volumen.ViajesCancelados));
        Assert.Equal(new IndicadoresCumplimiento(1, 0, 0.0m, 20.0m, 0, 10), octubre.Cumplimiento);
        Assert.Equal((1, 2, (decimal?)50.0m, (decimal?)0.0m, (decimal?)50.0m, (decimal?)4.0m), (octubre.Conductores.ConductoresConAsignaciones,
            octubre.Conductores.Asignaciones, octubre.Conductores.PorcentajeAceptacion, octubre.Conductores.PorcentajeRechazo,
            octubre.Conductores.PorcentajeVencidas, octubre.Conductores.CalificacionPromedio));
        Assert.Equal(conductor, Assert.Single(octubre.Conductores.MasViajes).ConductorId);
        Assert.Equal(2, octubre.Empresas.Count);

        Assert.Equal(1, _cache.Creaciones);
        Assert.Same(septiembre, otraVez);
        Assert.Equal(1, septiembre.Volumen.Solicitudes);
    }

    [Fact]
    public async Task VistaCentralizada_DeHoy_PrimeroLosCriticos()
    {
        var hoy = PeriodoReporte.DelDia(new DateOnly(2026, 10, 18), Bogota);
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Ahora.AddHours(-2)));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Pendiente, Ahora.AddHours(3)));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Asignada, Ahora.AddMinutes(-15), asignacion: EstadoAsignacion.Aceptada,
            viaje: EstadoViaje.Programado));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.SinCandidatos, Ahora.AddHours(5), personal: true));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Pendiente, hoy.Hasta.AddHours(1)));

        var vista = await new VistaCentralizadaCasoUso(Generador, Bogota, Reloj).EjecutarAsync(
            new VistaCentralizadaDominio(null, null, LectorSolicitud.Administrador), CancellationToken.None);

        Assert.Equal((hoy.Desde, hoy.Hasta), (vista.Desde, vista.Hasta));
        Assert.Equal((2, 1, 1), (vista.Criticos, vista.Atencion, vista.Normales));
        Assert.Equal([NivelAtencion.Critico, NivelAtencion.Critico, NivelAtencion.Atencion, NivelAtencion.Normal], vista.Servicios.Select(s => s.Nivel));
        Assert.Equal(EstadoSolicitud.Asignada, vista.Servicios[0].Servicio.Estado);
        Assert.True(vista.Servicios[1].Servicio.DetalleRestringido);
    }

    private ExportarReporteCasoUso Exportar => new(Generador, _almacen, _auditoria, _unidadTrabajo, Bogota, Reloj);

    private static string Texto(byte[] contenido)
    {
        Assert.Equal(Encoding.UTF8.GetPreamble(), contenido[..3]);
        return Encoding.UTF8.GetString(contenido[3..]);
    }

    [Fact]
    public async Task Exportar_SubeElCsvAExports_AuditaYDevuelveUnEnlaceDe15Minutos()
    {
        _viajes.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora, retraso: 5, puntaje: 5));
        var actor = Guid.NewGuid();

        var exporte = await Exportar.EjecutarAsync(new ExportarReporteDominio(TipoReporte.Viajes, new FiltroReporte(Octubre),
            LectorSolicitud.OperadorLogistico, actor), CancellationToken.None);

        var (ruta, (contenido, tipo)) = Assert.Single(_almacen.Objetos);
        Assert.Equal($"exports/viajes/{exporte.Id}/reporte-viajes-20261001-20261031.csv", ruta);
        Assert.Equal(("reporte-viajes-20261001-20261031.csv", 1, Ahora.AddMinutes(15)), (exporte.NombreArchivo, exporte.Filas, exporte.ExpiraEn));
        Assert.Contains("vence=15", exporte.Url.ToString(), StringComparison.Ordinal);
        Assert.StartsWith("text/csv", tipo, StringComparison.Ordinal);

        var lineas = Texto(contenido).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lineas.Length);
        Assert.StartsWith("Viaje;Solicitud;Inicio programado;", lineas[0], StringComparison.Ordinal);
        Assert.Contains(";2026-10-18 07:00;2026-10-18 07:05;", lineas[1], StringComparison.Ordinal);
        Assert.Contains(";Sí;5;Carlos;ABC123;'=Ana;;\"Hotel; \"\"Central\"\"\";Aeropuerto;No;No;0;5", lineas[1], StringComparison.Ordinal);

        var evento = Assert.Single(_auditoria.Eventos);
        Assert.Equal(("reporte.exportar", (Guid?)actor, exporte.Id.ToString()), (evento.Accion, evento.ActorId, evento.RecursoId));
    }

    [Theory]
    [InlineData(TipoReporte.Solicitudes, "Solicitud;Grupo;Estado;")]
    [InlineData(TipoReporte.Conductores, "Conductor;Nombre;Placas;")]
    [InlineData(TipoReporte.Empresas, "Empresa;Nombre;Solicitudes;")]
    public async Task Exportar_CadaTipoTieneSusColumnas(TipoReporte tipoReporte, string encabezado)
    {
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Ahora, conEmpresa: true, asignacion: EstadoAsignacion.Finalizada,
            viaje: EstadoViaje.Finalizado, puntaje: 4));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Ahora, conEmpresa: true, puntaje: 5));
        var conductor = Guid.NewGuid();
        _consultas.Asignaciones.Add(Asignacion(conductor, "Luis", EstadoAsignacion.Finalizada, viaje: EstadoViaje.Finalizado, retraso: 0));
        _consultas.Asignaciones.Add(Asignacion(conductor, "Luis", EstadoAsignacion.Rechazada));
        _consultas.Asignaciones.Add(Asignacion(conductor, "Luis", EstadoAsignacion.Rechazada));

        var exporte = await Exportar.EjecutarAsync(new ExportarReporteDominio(tipoReporte, new FiltroReporte(Octubre),
            LectorSolicitud.Administrador, Guid.NewGuid()), CancellationToken.None);

        var texto = Texto(Assert.Single(_almacen.Objetos).Value.Contenido);
        Assert.StartsWith(encabezado, texto, StringComparison.Ordinal);
        Assert.True(exporte.Filas >= 1);
        if (tipoReporte == TipoReporte.Conductores) Assert.Contains(";33,3;", texto, StringComparison.Ordinal);
        if (tipoReporte == TipoReporte.Empresas) Assert.Contains(";4,5", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exportar_SiLaAuditoriaFalla_BorraElArchivo()
    {
        _unidadTrabajo.Falla = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => Exportar.EjecutarAsync(new ExportarReporteDominio(TipoReporte.Empresas,
            new FiltroReporte(Octubre), LectorSolicitud.Administrador, Guid.NewGuid()), CancellationToken.None));

        Assert.Empty(_almacen.Objetos);
    }

    [Fact]
    public async Task Reporte_ConDemasiadasFilas_PideAcotarElPeriodo()
    {
        for (var i = 0; i <= ReglasReportes.FilasMaximas; i++)
            _consultas.Asignaciones.Add(Asignacion(Guid.Empty, "Luis", EstadoAsignacion.Asignada));

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ReporteConductoresCasoUso(Generador).EjecutarAsync(
            new ConsultarReporteDominio(new FiltroReporte(Octubre), Paginacion.PorDefecto, LectorSolicitud.Administrador), CancellationToken.None));
    }

    private ReportesAdaptadorEntrada Adaptador => new(new VistaCentralizadaCasoUso(Generador, Bogota, Reloj),
        new ConsultarIndicadoresCasoUso(Generador, _cache, Reloj), new ReporteViajesCasoUso(Generador), new ReporteSolicitudesCasoUso(Generador),
        new ReporteConductoresCasoUso(Generador), new ReporteEmpresasCasoUso(Generador), Exportar);

    private static ReportePeticion Peticion(string? grupo = null, int dias = 30) => new()
    {
        Desde = Octubre.Desde,
        Hasta = Octubre.Desde.AddDays(dias),
        Grupo = grupo,
        Pagina = 1,
        TamanoPagina = 10
    };

    [Fact]
    public async Task Adaptador_ValidaPeriodoGrupoTipoYActor()
    {
        var adaptador = Adaptador;

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ViajesAsync(new ReportePeticion(), RolesPanel.Administrador, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.IndicadoresAsync(Peticion(dias: 94), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.SolicitudesAsync(Peticion("Perdida"), RolesPanel.Administrador, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ExportarAsync("facturas", Peticion(), RolesPanel.Administrador, Guid.NewGuid(),
            CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ExportarAsync("viajes", Peticion(), RolesPanel.Administrador, null,
            CancellationToken.None));
    }

    [Fact]
    public async Task Adaptador_DevuelveLasPaginasYElExporte()
    {
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Pendiente, Octubre.Desde.AddDays(1), personal: true));
        _consultas.Solicitudes.Add(Solicitud(EstadoSolicitud.Completada, Octubre.Desde.AddDays(1), conEmpresa: true));
        var adaptador = Adaptador;

        var solicitudes = await adaptador.SolicitudesAsync(Peticion("pendiente"), RolesPanel.OperadorLogistico, CancellationToken.None);
        var conductores = await adaptador.ConductoresAsync(Peticion(), CancellationToken.None);
        var empresas = await adaptador.EmpresasAsync(Peticion(), CancellationToken.None);
        var viajes = await adaptador.ViajesAsync(Peticion(), RolesPanel.Administrador, CancellationToken.None);
        var indicadores = await adaptador.IndicadoresAsync(Peticion(), CancellationToken.None);
        var vista = await adaptador.VistaCentralizadaAsync(new VistaCentralizadaPeticion { Fecha = new DateOnly(2026, 10, 2) },
            RolesPanel.OperadorLogistico, CancellationToken.None);
        var exporte = await adaptador.ExportarAsync("Solicitudes", Peticion(), RolesPanel.OperadorLogistico, Guid.NewGuid(), CancellationToken.None);

        var fila = Assert.Single(solicitudes.Filas.Elementos);
        Assert.False(fila.Solicitud.DetalleRestringido);
        Assert.Equal((1, 10, 2), (solicitudes.Filas.Pagina, solicitudes.Filas.TamanoPagina, solicitudes.Resumen.Total));
        Assert.Empty(conductores.Filas.Elementos);
        Assert.Equal(2, empresas.Filas.Total);
        Assert.Equal(0, viajes.Resumen.Viajes);
        Assert.Equal(2, indicadores.Volumen.Solicitudes);
        Assert.Equal(2, vista.Servicios.Count);
        Assert.Equal(("Solicitudes", 2), (exporte.Tipo, exporte.Filas));
    }
}
