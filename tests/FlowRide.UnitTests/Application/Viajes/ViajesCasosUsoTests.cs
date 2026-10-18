using FlowRide.Application.Asignaciones.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Solicitudes.CasosUso;
using FlowRide.Application.Viajes.CasosUso;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes;
using FlowRide.Domain.Viajes.GestionViajes;
using FlowRide.UnitTests.Application.Asignaciones;
using FlowRide.UnitTests.Application.Operacion;
using FlowRide.UnitTests.Application.Solicitudes;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Viajes;

public class ViajesCasosUsoTests : CasosUsoOperacionBase
{
    private static readonly DateTimeOffset Servicio = Ahora.AddDays(1);
    private static readonly OperadorDelStack Operador = new("tenant-a");
    private static readonly AssignmentEngine Motor = new(DefaultAssignmentRules.Create());

    private readonly SolicitudesEnMemoria _solicitudes;
    private readonly ConfiguracionesEnMemoria _configuraciones = new ConfiguracionesEnMemoria().Con(ConfiguracionesEnMemoria.Nueva(1, activa: true));
    private readonly DecisionesEnMemoria _decisiones = new();
    private readonly BandejaEnMemoria _bandeja = new();
    private readonly ParametrosEnMemoria _parametros = new();
    private readonly CalificacionesEnMemoria _calificaciones = new();
    private readonly Pasajero _pasajero;

    public ViajesCasosUsoTests()
    {
        _solicitudes = new SolicitudesEnMemoria(Pasajeros, Empresas);
        var empresa = Empresas.Registrar();
        _pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", empresa.Id);
        Pasajeros.Elementos[_pasajero.Id] = _pasajero;
    }

    private ViajeDominio ViajeDominio => new(Viajes, _calificaciones, new ParametrosDominio(_parametros));

    private DevolucionPorRechazo Devolucion => new(_solicitudes, Auditoria, _bandeja);

    private AsignarSolicitudCasoUso Asignar() => new(_solicitudes, _configuraciones, Conductores, Asignaciones, _decisiones,
        new PreparadorDecision(Conductores, Vehiculos, Asignaciones), Motor, Operador, _bandeja, UnidadTrabajo, Reloj);

    private AceptarAsignacionCasoUso Aceptar() => new(Asignaciones, Viajes, Auditoria, _bandeja, UnidadTrabajo, Reloj);

    private IniciarViajeCasoUso Iniciar(TimeProvider? reloj = null) =>
        new(ViajeDominio, Asignaciones, _solicitudes, Auditoria, _bandeja, UnidadTrabajo, reloj ?? Reloj);

    private FinalizarViajeCasoUso Finalizar(TimeProvider? reloj = null) =>
        new(ViajeDominio, Asignaciones, _solicitudes, Auditoria, _bandeja, UnidadTrabajo, reloj ?? Reloj);

    private CalificarViajeCasoUso Calificar(TimeProvider reloj) =>
        new(ViajeDominio, Asignaciones, _solicitudes, Pasajeros, _calificaciones, Auditoria, UnidadTrabajo, reloj);

    private SolicitudViaje Solicitud()
    {
        var solicitud = new SolicitudViaje(Guid.NewGuid(), _pasajero.Id, "Hotel", "Aeropuerto", Servicio, TimeSpan.FromMinutes(45), 2,
            CanalSolicitud.PanelWeb, Ahora, empresaClienteId: _pasajero.EmpresaClienteId);
        _solicitudes.Solicitudes[solicitud.Id] = solicitud;
        return solicitud;
    }

    private Conductor ConductorConVehiculo(string licencia)
    {
        var conductor = ConductorRegistrado(licencia);
        conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Servicio.AddHours(-2), Servicio.AddHours(6)));
        var vehiculo = new Vehiculo(Guid.NewGuid(), $"P{licencia}", "Toyota", "Hiace", 10, Vigente, Vigente, conductorHabitualId: conductor.Id);
        Vehiculos.Elementos[vehiculo.Id] = vehiculo;
        return conductor;
    }

    private async Task<(SolicitudViaje Solicitud, Asignacion Asignacion)> AsignadaAsync()
    {
        ConductorConVehiculo("1001");
        var solicitud = Solicitud();
        var resultado = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);
        return (solicitud, Asignaciones.Todas[resultado.AsignacionId!.Value]);
    }

    private async Task<(SolicitudViaje Solicitud, Asignacion Asignacion, Viaje Viaje)> AceptadaAsync()
    {
        var (solicitud, asignacion) = await AsignadaAsync();
        var viaje = await Aceptar().EjecutarAsync(new AceptarAsignacionDominio(asignacion.Id, Actor), CancellationToken.None);
        return (solicitud, asignacion, viaje);
    }

    private async Task<(SolicitudViaje Solicitud, Viaje Viaje)> FinalizadoAsync()
    {
        var (solicitud, _, viaje) = await AceptadaAsync();
        await Iniciar(new RelojFijo(Servicio)).EjecutarAsync(new IniciarViajeDominio(viaje.Id, Actor), CancellationToken.None);
        await Finalizar(new RelojFijo(Servicio.AddMinutes(50))).EjecutarAsync(new FinalizarViajeDominio(viaje.Id, Actor), CancellationToken.None);
        return (solicitud, viaje);
    }

    [Fact]
    public async Task Aceptar_CreaElViajeProgramado_LoAuditaYDejaElEvento()
    {
        var (solicitud, asignacion) = await AsignadaAsync();

        var viaje = await Aceptar().EjecutarAsync(new AceptarAsignacionDominio(asignacion.Id, Actor), CancellationToken.None);

        Assert.Equal((EstadoAsignacion.Aceptada, EstadoViaje.Programado, asignacion.Id), (asignacion.Estado, viaje.Estado, viaje.AsignacionId));
        Assert.Same(viaje, Assert.Single(Viajes.Todos.Values));
        Assert.Equal(EstadoSolicitud.Asignada, solicitud.Estado);
        Assert.Equal("asignacion.aceptar", UltimaAccion);
        Assert.Equal(EventosSalida.AsignacionAceptada, _bandeja.Eventos[^1].Tipo);
    }

    [Fact]
    public async Task Aceptar_DosVeces_EsUnaReglaDeNegocio()
    {
        var (_, asignacion, _) = await AceptadaAsync();

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Aceptar().EjecutarAsync(new AceptarAsignacionDominio(asignacion.Id, Actor), CancellationToken.None));
        Assert.Contains("Aceptada", error.Message);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Aceptar().EjecutarAsync(new AceptarAsignacionDominio(Guid.NewGuid(), Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Rechazar_DevuelveLaSolicitudALaCola_YElMotorNoVuelveAProponerAlConductor()
    {
        var (solicitud, asignacion) = await AsignadaAsync();
        var casoUso = new RechazarAsignacionCasoUso(Asignaciones, Devolucion, UnidadTrabajo, Reloj);

        await casoUso.EjecutarAsync(new RechazarAsignacionDominio(asignacion.Id, " Vehículo en taller ", Actor), CancellationToken.None);

        Assert.Equal((EstadoAsignacion.Rechazada, "Vehículo en taller"), (asignacion.Estado, asignacion.MotivoCierre));
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal("Asignación rechazada: Vehículo en taller", solicitud.Historial[^1].Motivo);
        Assert.Equal("asignacion.rechazar", UltimaAccion);
        Assert.Equal(EventosSalida.AsignacionRechazada, _bandeja.Eventos[^1].Tipo);

        // El único conductor la rechazó: el motor ya no lo propone.
        var nuevo = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);
        Assert.Equal(AssignmentOutcome.NoCandidates, nuevo.Resultado);
        var evaluacion = Assert.Single(_decisiones.Decisiones[^1].Evaluaciones);
        Assert.Contains("rechazó", evaluacion.MotivoDescarte);
    }

    [Fact]
    public async Task Rechazar_ConOtroConductorDisponible_LaReasignaAEse()
    {
        var (solicitud, asignacion) = await AsignadaAsync();
        var otro = ConductorConVehiculo("2002");
        await new RechazarAsignacionCasoUso(Asignaciones, Devolucion, UnidadTrabajo, Reloj)
            .EjecutarAsync(new RechazarAsignacionDominio(asignacion.Id, "No puedo", Actor), CancellationToken.None);

        var nuevo = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);

        Assert.Equal((AssignmentOutcome.Assigned, otro.Id), (nuevo.Resultado, nuevo.ConductorId));
    }

    [Fact]
    public async Task Rechazar_SinMotivo_OYaAceptada_Falla()
    {
        var (_, asignacion, _) = await AceptadaAsync();
        var casoUso = new RechazarAsignacionCasoUso(Asignaciones, Devolucion, UnidadTrabajo, Reloj);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            casoUso.EjecutarAsync(new RechazarAsignacionDominio(asignacion.Id, " ", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            casoUso.EjecutarAsync(new RechazarAsignacionDominio(asignacion.Id, "Tarde", Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Vencer_DespuesDelPlazo_TrataLaAsignacionComoRechazada()
    {
        var (solicitud, asignacion) = await AsignadaAsync();
        var despues = new RelojFijo(Ahora.AddMinutes(15));
        var antes = new RelojFijo(Ahora.AddMinutes(14));

        Assert.Empty(await new ConsultarSinRespuestaCasoUso(Asignaciones, ViajeDominio, antes)
            .EjecutarAsync(new ConsultarSinRespuestaDominio(10), CancellationToken.None));
        Assert.False(await new VencerAsignacionCasoUso(Asignaciones, ViajeDominio, Devolucion, UnidadTrabajo, antes)
            .EjecutarAsync(new VencerAsignacionDominio(asignacion.Id), CancellationToken.None));

        var pendientes = await new ConsultarSinRespuestaCasoUso(Asignaciones, ViajeDominio, despues)
            .EjecutarAsync(new ConsultarSinRespuestaDominio(10), CancellationToken.None);
        Assert.Equal([asignacion.Id], pendientes);

        var vencio = await new VencerAsignacionCasoUso(Asignaciones, ViajeDominio, Devolucion, UnidadTrabajo, despues)
            .EjecutarAsync(new VencerAsignacionDominio(asignacion.Id), CancellationToken.None);

        Assert.True(vencio);
        Assert.Equal((EstadoAsignacion.Rechazada, "El conductor no respondió en 15 minutos."), (asignacion.Estado, asignacion.MotivoCierre));
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        var evento = Auditoria.Eventos[^1];
        Assert.Equal(("asignacion.vencer", (Guid?)null), (evento.Accion, evento.ActorId));
    }

    [Fact]
    public async Task Vencer_UsaElPlazoDelParametro_YNoVenceLasAceptadas()
    {
        _parametros.Parametros[CatalogoParametros.PlazoRespuestaAsignacionMinutos] =
            new Parametro(CatalogoParametros.Buscar(CatalogoParametros.PlazoRespuestaAsignacionMinutos), "5", Actor, Ahora);
        var (_, asignacion, _) = await AceptadaAsync();

        Assert.False(await new VencerAsignacionCasoUso(Asignaciones, ViajeDominio, Devolucion, UnidadTrabajo, new RelojFijo(Ahora.AddHours(1)))
            .EjecutarAsync(new VencerAsignacionDominio(asignacion.Id), CancellationToken.None));
        Assert.Equal(TimeSpan.FromMinutes(5), await ViajeDominio.PlazoRespuestaAsync(CancellationToken.None));
    }

    [Fact]
    public async Task IniciarYFinalizar_MuevenViajeAsignacionYSolicitud()
    {
        var (solicitud, asignacion, viaje) = await AceptadaAsync();

        await Iniciar(new RelojFijo(Servicio)).EjecutarAsync(new IniciarViajeDominio(viaje.Id, Actor), CancellationToken.None);
        Assert.Equal((EstadoViaje.EnCurso, EstadoAsignacion.EnCurso, EstadoSolicitud.EnCurso, (DateTimeOffset?)Servicio),
            (viaje.Estado, asignacion.Estado, solicitud.Estado, viaje.FechaHoraInicio));
        Assert.Equal("viaje.iniciar", UltimaAccion);
        Assert.Equal(EventosSalida.ViajeIniciado, _bandeja.Eventos[^1].Tipo);

        await Finalizar(new RelojFijo(Servicio.AddMinutes(50))).EjecutarAsync(new FinalizarViajeDominio(viaje.Id, Actor), CancellationToken.None);
        Assert.Equal((EstadoViaje.Finalizado, EstadoAsignacion.Finalizada, EstadoSolicitud.Completada),
            (viaje.Estado, asignacion.Estado, solicitud.Estado));
        Assert.Equal("viaje.finalizar", UltimaAccion);
        Assert.Equal(EventosSalida.ViajeFinalizado, _bandeja.Eventos[^1].Tipo);
    }

    [Fact]
    public async Task Finalizar_SinIniciar_OViajeInexistente_Falla()
    {
        var (_, _, viaje) = await AceptadaAsync();

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Finalizar().EjecutarAsync(new FinalizarViajeDominio(viaje.Id, Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Iniciar().EjecutarAsync(new IniciarViajeDominio(Guid.NewGuid(), Actor), CancellationToken.None));
    }

    [Fact]
    public async Task ParadasEIncidentes_SeRegistranEnElViaje()
    {
        var (solicitud, asignacion, viaje) = await AceptadaAsync();
        var parada = await new RegistrarParadaCasoUso(ViajeDominio, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new RegistrarParadaDominio(viaje.Id, "Terminal 2", 1, Servicio.AddMinutes(20), Actor), CancellationToken.None);
        var incidente = await new ReportarIncidenteCasoUso(ViajeDominio, Asignaciones, Auditoria, _bandeja, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ReportarIncidenteDominio(viaje.Id, TipoIncidente.Retraso, "Trancón en la autopista", Actor), CancellationToken.None);

        Assert.Equal((viaje.Id, 1), (parada.ViajeId, parada.Orden));
        Assert.Equal((TipoIncidente.Retraso, Actor), (incidente.Tipo, incidente.ReportadoPorId));
        Assert.Equal(EventosSalida.IncidenteReportado, _bandeja.Eventos[^1].Tipo);
        Assert.Equal("viaje.reportar_incidente", UltimaAccion);

        // La llegada solo se registra con el viaje en curso.
        var llegada = new RegistrarLlegadaCasoUso(ViajeDominio, Auditoria, UnidadTrabajo, new RelojFijo(Servicio.AddMinutes(25)));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            llegada.EjecutarAsync(new RegistrarLlegadaDominio(viaje.Id, parada.Id, Actor), CancellationToken.None));
        await Iniciar(new RelojFijo(Servicio)).EjecutarAsync(new IniciarViajeDominio(viaje.Id, Actor), CancellationToken.None);
        var conLlegada = await llegada.EjecutarAsync(new RegistrarLlegadaDominio(viaje.Id, parada.Id, Actor), CancellationToken.None);
        Assert.Equal(Servicio.AddMinutes(25), conLlegada.HoraReal);
        Assert.Equal((EstadoSolicitud.EnCurso, EstadoAsignacion.EnCurso), (solicitud.Estado, asignacion.Estado));

        // Orden repetido: regla de negocio.
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new RegistrarParadaCasoUso(ViajeDominio, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new RegistrarParadaDominio(viaje.Id, "Otra", 1, Servicio, Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Calificar_UnaVezDentroDelPlazo()
    {
        var (_, viaje) = await FinalizadoAsync();
        var fin = viaje.FechaHoraFin!.Value;

        var calificacion = await Calificar(new RelojFijo(fin.AddDays(1)))
            .EjecutarAsync(new CalificarViajeDominio(viaje.Id, 5, " Muy puntual ", Actor), CancellationToken.None);

        Assert.Equal((viaje.Id, _pasajero.Id, 5, "Muy puntual"), (calificacion.ViajeId, calificacion.PasajeroId, calificacion.Puntaje, calificacion.Comentario));
        Assert.Equal("calificacion.crear", UltimaAccion);
        var repetida = await Assert.ThrowsAsync<FlowRideExcepcion>(() => Calificar(new RelojFijo(fin.AddDays(2)))
            .EjecutarAsync(new CalificarViajeDominio(viaje.Id, 4, null, Actor), CancellationToken.None));
        Assert.Contains("ya calificó", repetida.Message);
    }

    [Fact]
    public async Task Calificar_FueraDelPlazo_OSinTerminar_Falla()
    {
        var (_, _, programado) = await AceptadaAsync();
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Calificar(Reloj)
            .EjecutarAsync(new CalificarViajeDominio(programado.Id, 5, null, Actor), CancellationToken.None));

        var (_, viaje) = await FinalizadoAsync();
        var vencida = await Assert.ThrowsAsync<FlowRideExcepcion>(() => Calificar(new RelojFijo(viaje.FechaHoraFin!.Value.AddDays(8)))
            .EjecutarAsync(new CalificarViajeDominio(viaje.Id, 5, null, Actor), CancellationToken.None));
        Assert.Contains("7 días", vencida.Message);
    }

    [Fact]
    public async Task ReasignarUnaAceptada_CancelaSuViajeProgramado()
    {
        var (_, asignacion, viaje) = await AceptadaAsync();
        var otro = ConductorConVehiculo("2002");
        var vehiculo = Vehiculos.Elementos.Values.Single(v => v.ConductorHabitualId == otro.Id);
        var validador = new ValidadorCandidatoManual(_configuraciones, Conductores, Vehiculos, _decisiones,
            new PreparadorDecision(Conductores, Vehiculos, Asignaciones), Motor, Operador);

        await new ReasignarCasoUso(Asignaciones, Viajes, _solicitudes, validador, Auditoria, _bandeja, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ReasignarDominio(asignacion.Id, otro.Id, vehiculo.Id, "Conductor enfermo", Actor), CancellationToken.None);

        Assert.Equal((EstadoAsignacion.Reemplazada, EstadoViaje.Cancelado), (asignacion.Estado, viaje.Estado));
    }

    [Fact]
    public async Task CancelarLaSolicitudDeUnaAceptada_CancelaSuViajeProgramado()
    {
        var (solicitud, asignacion, viaje) = await AceptadaAsync();

        await new CancelarSolicitudCasoUso(new SolicitudDominio(_solicitudes, new ParametrosDominio(_parametros)), _solicitudes, Asignaciones, Viajes,
                Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Vuelo cancelado", Actor, true), CancellationToken.None);

        Assert.Equal((EstadoAsignacion.Cancelada, EstadoViaje.Cancelado), (asignacion.Estado, viaje.Estado));
    }

    [Fact]
    public async Task AbandonarElTurno_CancelaLosViajesProgramados()
    {
        var (solicitud, asignacion, viaje) = await AceptadaAsync();
        var conductor = Conductores.Elementos[asignacion.ConductorId];

        await new AbandonarTurnoCasoUso(ConductorDominio, Conductores, Asignaciones, Viajes, _solicitudes, Auditoria, _bandeja, UnidadTrabajo, Reloj)
            .EjecutarAsync(new AbandonarTurnoDominio(conductor.Id, conductor.Turnos[0].Id, "Emergencia", Actor), CancellationToken.None);

        Assert.Equal((EstadoAsignacion.Cancelada, EstadoViaje.Cancelado, EstadoSolicitud.Pendiente), (asignacion.Estado, viaje.Estado, solicitud.Estado));
    }
}

internal sealed class ConsultasViajesEnMemoria : IConsultasViajes
{
    public List<ViajeLectura> Viajes { get; } = [];
    public TrazabilidadLectura? Trazabilidad { get; set; }
    public DateTimeOffset? ProgramadosHasta { get; private set; }

    public Task<PaginaResultado<ViajeLectura>> BuscarAsync(FiltroViajes filtro, Paginacion paginacion, CancellationToken ct) =>
        Task.FromResult(new PaginaResultado<ViajeLectura>(Viajes, Viajes.Count, paginacion));

    public Task<ViajeDetalleLectura?> DetalleAsync(Guid viajeId, CancellationToken ct) =>
        Task.FromResult(Viajes.Where(v => v.Id == viajeId)
            .Select(v => new ViajeDetalleLectura(v, [new ParadaLectura(Guid.NewGuid(), 1, "Terminal 2", v.InicioProgramado, null)], [], null))
            .FirstOrDefault());

    public Task<IReadOnlyList<ViajeLectura>> ActivosAsync(DateTimeOffset programadosHasta, CancellationToken ct)
    {
        ProgramadosHasta = programadosHasta;
        return Task.FromResult<IReadOnlyList<ViajeLectura>>(Viajes);
    }

    public Task<TrazabilidadLectura?> TrazabilidadAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult(Trazabilidad?.SolicitudId == solicitudId ? Trazabilidad : null);
}

public class ConsultasViajesCasosUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private readonly ConsultasViajesEnMemoria _consultas = new();
    private readonly ParametrosEnMemoria _parametros = new();

    private ParametrosDominio Parametros => new(_parametros);

    private static ViajeLectura Viaje(EstadoViaje estado, DateTimeOffset inicio, bool personal = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), estado, Guid.NewGuid(), "Carlos", Guid.NewGuid(), "ABC123", Guid.NewGuid(), "Ana",
            null, null, "Hotel", "Aeropuerto", false, personal, inicio, inicio.AddMinutes(45), null, null, 0, null);

    [Fact]
    public async Task Buscar_OcultaLasDireccionesDeLasPersonales_AlAdministrador()
    {
        _consultas.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora, personal: true));
        _consultas.Viajes.Add(Viaje(EstadoViaje.Finalizado, Ahora));
        var casoUso = new BuscarViajesCasoUso(_consultas, Parametros);

        var administrador = await casoUso.EjecutarAsync(new BuscarViajesDominio(new FiltroViajes(), Paginacion.PorDefecto,
            LectorSolicitud.Administrador), CancellationToken.None);
        var operador = await casoUso.EjecutarAsync(new BuscarViajesDominio(new FiltroViajes(), Paginacion.PorDefecto,
            LectorSolicitud.OperadorLogistico), CancellationToken.None);

        Assert.Equal([true, false], administrador.Elementos.Select(v => v.DetalleRestringido));
        Assert.Null(administrador.Elementos[0].DireccionOrigen);
        Assert.All(operador.Elementos, v => Assert.NotNull(v.DireccionOrigen));
    }

    [Fact]
    public async Task Consultar_PersonalSinPermiso_OcultaTambienLasParadas()
    {
        var personal = Viaje(EstadoViaje.EnCurso, Ahora, personal: true);
        _consultas.Viajes.Add(personal);
        var casoUso = new ConsultarViajeCasoUso(_consultas, Parametros);

        var detalle = await casoUso.EjecutarAsync(new ConsultarViajeDominio(personal.Id, LectorSolicitud.Administrador), CancellationToken.None);

        Assert.True(detalle.Viaje.DetalleRestringido);
        Assert.Null(Assert.Single(detalle.Paradas).Direccion);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            casoUso.EjecutarAsync(new ConsultarViajeDominio(Guid.NewGuid(), LectorSolicitud.Administrador), CancellationToken.None));
    }

    [Fact]
    public async Task Monitorear_MarcaRetrasadosLosProgramadosCuyaHoraPaso()
    {
        _consultas.Viajes.Add(Viaje(EstadoViaje.Programado, Ahora.AddMinutes(-20)));
        _consultas.Viajes.Add(Viaje(EstadoViaje.Programado, Ahora.AddMinutes(30)));
        _consultas.Viajes.Add(Viaje(EstadoViaje.EnCurso, Ahora.AddMinutes(-40)));
        var casoUso = new MonitorearViajesCasoUso(_consultas, Parametros, new RelojFijo(Ahora));

        var monitoreo = await casoUso.EjecutarAsync(new MonitorearViajesDominio(LectorSolicitud.OperadorLogistico), CancellationToken.None);

        Assert.Equal([(true, 20), (false, 0), (false, 0)], monitoreo.Select(m => (m.Retrasado, m.MinutosRetraso)));
        Assert.Equal(Ahora + MonitorearViajesCasoUso.HorizonteProgramados, _consultas.ProgramadosHasta);
    }

    [Fact]
    public async Task Trazabilidad_DeUnaSolicitudInexistente_Falla()
    {
        var solicitudId = Guid.NewGuid();
        _consultas.Trazabilidad = new TrazabilidadLectura(solicitudId, EstadoSolicitud.Completada,
            [new EventoTrazabilidad(Ahora, "solicitud", "Solicitud registrada (Pendiente).", null, null, solicitudId)]);
        var casoUso = new TrazabilidadSolicitudCasoUso(_consultas);

        Assert.Single((await casoUso.EjecutarAsync(new TrazabilidadSolicitudDominio(solicitudId, LectorSolicitud.Administrador),
            CancellationToken.None)).Eventos);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            casoUso.EjecutarAsync(new TrazabilidadSolicitudDominio(Guid.NewGuid(), LectorSolicitud.Administrador), CancellationToken.None));
    }
}
