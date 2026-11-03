using FlowRide.Application.Asignaciones.CasosUso;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Asignaciones.Repositorios;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.UnitTests.Application.Operacion;
using FlowRide.UnitTests.Application.Solicitudes;

namespace FlowRide.UnitTests.Application.Asignaciones;

internal sealed class ConfiguracionesEnMemoria : IConfiguracionAsignacionRepositorio
{
    public Dictionary<Guid, ConfiguracionAsignacion> Configuraciones { get; } = [];

    public static ConfiguracionAsignacion Nueva(int version, IEnumerable<ReglaPedida>? reglas = null, bool activa = false)
    {
        var configuracion = new ConfiguracionAsignacion(Guid.NewGuid(), version, reglas ?? ConfiguracionInicial.Reglas, DefaultAssignmentRules.Create(),
            null, DateTimeOffset.UnixEpoch);
        if (activa) configuracion.Activar(DateTimeOffset.UnixEpoch);
        return configuracion;
    }

    public ConfiguracionesEnMemoria Con(ConfiguracionAsignacion configuracion)
    {
        Configuraciones[configuracion.Id] = configuracion;
        return this;
    }

    public Task CrearAsync(ConfiguracionAsignacion entidad, CancellationToken ct) => Task.FromResult(Con(entidad));
    public Task ModificarAsync(Guid id, ConfiguracionAsignacion entidad, CancellationToken ct) => Task.CompletedTask;
    public Task<ConfiguracionAsignacion?> ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Configuraciones.GetValueOrDefault(id));

    public Task<IReadOnlyList<ConfiguracionAsignacion>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ConfiguracionAsignacion>>(Configuraciones.Values.ToList());

    public Task<ConfiguracionAsignacion?> ConsultarActivaAsync(CancellationToken ct) =>
        Task.FromResult(Configuraciones.Values.SingleOrDefault(c => c.Estado == EstadoConfiguracion.Activa));

    public Task<ConfiguracionAsignacion?> ConsultarPorVersionAsync(int version, CancellationToken ct) =>
        Task.FromResult(Configuraciones.Values.SingleOrDefault(c => c.Version == version));

    public Task<int> UltimaVersionAsync(CancellationToken ct) => Task.FromResult(Configuraciones.Values.Select(c => c.Version).DefaultIfEmpty().Max());
}

internal sealed class DecisionesEnMemoria : IDecisionAsignacionRepositorio
{
    public List<DecisionAsignacion> Decisiones { get; } = [];

    public Task CrearAsync(DecisionAsignacion entidad, CancellationToken ct)
    {
        Decisiones.Add(entidad);
        return Task.CompletedTask;
    }
}

internal sealed class BandejaEnMemoria : IBandejaSalida
{
    public List<(string Tipo, object Contenido)> Eventos { get; } = [];

    public Task AgregarAsync(string tipoEvento, object contenido, CancellationToken ct)
    {
        Eventos.Add((tipoEvento, contenido));
        return Task.CompletedTask;
    }

    public List<(string Tipo, object Contenido, DateTimeOffset Cuando)> Programados { get; } = [];

    public Task ProgramarAsync(string tipoEvento, object contenido, DateTimeOffset cuando, CancellationToken ct)
    {
        Programados.Add((tipoEvento, contenido, cuando));
        return Task.CompletedTask;
    }
}

public class AsignacionCasosUsoTests : CasosUsoOperacionBase
{
    private static readonly DateTimeOffset Servicio = Ahora.AddDays(1);
    private static readonly OperadorDelStack Operador = new("tenant-a");
    private static readonly AssignmentEngine Motor = new(DefaultAssignmentRules.Create());

    private readonly SolicitudesEnMemoria _solicitudes;
    private readonly ConfiguracionesEnMemoria _configuraciones = new ConfiguracionesEnMemoria().Con(ConfiguracionesEnMemoria.Nueva(1, activa: true));
    private readonly DecisionesEnMemoria _decisiones = new();
    private readonly BandejaEnMemoria _bandeja = new();
    private readonly Pasajero _pasajero;

    public AsignacionCasosUsoTests()
    {
        _solicitudes = new SolicitudesEnMemoria(Pasajeros, Empresas);
        var empresa = Empresas.Registrar();
        _pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", empresa.Id);
        Pasajeros.Elementos[_pasajero.Id] = _pasajero;
    }

    private PreparadorDecision Preparador => new(Conductores, Vehiculos, Asignaciones);

    private ValidadorCandidatoManual Validador => new(_configuraciones, Conductores, Vehiculos, _decisiones, Preparador, Motor, Operador);

    private AsignarSolicitudCasoUso Asignar() => new(_solicitudes, _configuraciones, Conductores, Asignaciones, _decisiones, Preparador, Motor,
        Operador, _bandeja, UnidadTrabajo, Reloj);

    private SolicitudViaje Solicitud(int pasajeros = 2, bool urgente = false)
    {
        var solicitud = new SolicitudViaje(Guid.NewGuid(), _pasajero.Id, "Hotel", "Aeropuerto", Servicio.Local(), TimeSpan.FromMinutes(45), pasajeros,
            CanalSolicitud.PanelWeb, Ahora, urgente, empresaClienteId: _pasajero.EmpresaClienteId);
        _solicitudes.Solicitudes[solicitud.Id] = solicitud;
        return solicitud;
    }

    private (Conductor Conductor, Vehiculo Vehiculo) ConductorConVehiculo(string licencia = "1001")
    {
        var conductor = ConductorRegistrado(licencia);
        conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Servicio.AddHours(-2), Servicio.AddHours(6)));
        var vehiculo = new Vehiculo(Guid.NewGuid(), $"P{licencia}", "Toyota", "Hiace", 10, Vigente, Vigente, conductorHabitualId: conductor.Id);
        Vehiculos.Elementos[vehiculo.Id] = vehiculo;
        return (conductor, vehiculo);
    }

    [Fact]
    public async Task Asignar_CreaLaAsignacion_GuardaLaDecision_YDejaElEventoEnLaOutbox()
    {
        var (conductor, vehiculo) = ConductorConVehiculo();
        var solicitud = Solicitud();

        var resultado = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);

        Assert.Equal((AssignmentOutcome.Assigned, conductor.Id, vehiculo.Id), (resultado.Resultado, resultado.ConductorId, resultado.VehiculoId));
        Assert.Equal(EstadoSolicitud.Asignada, solicitud.Estado);
        var asignacion = Assert.Single(Asignaciones.Todas.Values);
        Assert.Equal((OrigenAsignacion.Automatica, resultado.DecisionId), (asignacion.Origen, asignacion.DecisionId));
        var decision = Assert.Single(_decisiones.Decisiones);
        Assert.Equal((OrigenAsignacion.Automatica, Ahora, 1), (decision.Origen, decision.EvaluadaEn, decision.VersionConfiguracion));
        Assert.Equal(EventosSalida.AsignacionCreada, Assert.Single(_bandeja.Eventos).Tipo);
        Assert.Equal(Ahora, conductor.FechaUltimaAsignacion);
        Assert.Equal(1, UnidadTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Asignar_SinCandidatos_MarcaLaSolicitud_YAvisa()
    {
        var solicitud = Solicitud();

        var resultado = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);

        Assert.Equal(AssignmentOutcome.NoCandidates, resultado.Resultado);
        Assert.Equal(EstadoSolicitud.SinCandidatos, solicitud.Estado);
        Assert.Equal(EventosSalida.SolicitudSinCandidatos, Assert.Single(_bandeja.Eventos).Tipo);
        Assert.Empty(Asignaciones.Todas);
    }

    [Fact]
    public async Task Asignar_FueraDeLaCola_SinConfiguracionOConConfiguracionInvalida()
    {
        ConductorConVehiculo();
        var asignada = Solicitud();
        await Asignar().EjecutarAsync(new AsignarSolicitudDominio(asignada.Id), CancellationToken.None);

        var otraVez = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(asignada.Id), CancellationToken.None);
        Assert.Null(otraVez.Resultado);
        Assert.Contains("no está en la cola", otraVez.Motivo, StringComparison.Ordinal);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Asignar().EjecutarAsync(new AsignarSolicitudDominio(Guid.NewGuid()), CancellationToken.None));

        var activa = _configuraciones.Configuraciones.Values.Single();
        activa.Archivar();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Asignar().EjecutarAsync(new AsignarSolicitudDominio(Solicitud().Id), CancellationToken.None));

        var invalida = ConfiguracionesEnMemoria.Nueva(2, ConfiguracionInicial.Reglas.Where(r => r.Codigo != AvailabilityRule.RuleName), activa: true);
        _configuraciones.Con(invalida);
        var pendiente = Solicitud();
        pendiente.Modificar("Hotel", "Aeropuerto", Servicio.AddHours(3).Local(), TimeSpan.FromMinutes(30), 1, false, null, Ahora);
        var resultado = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(pendiente.Id), CancellationToken.None);
        Assert.Equal(AssignmentOutcome.InvalidConfiguration, resultado.Resultado);
        Assert.Equal(EstadoSolicitud.Pendiente, pendiente.Estado);
    }

    [Fact]
    public async Task Cola_UrgentesPrimero()
    {
        var normal = Solicitud();
        var urgente = Solicitud(urgente: true);

        var cola = await new ConsultarColaCasoUso(_solicitudes).EjecutarAsync(new ConsultarColaDominio(10), CancellationToken.None);

        Assert.Equal([urgente.Id, normal.Id], cola);
    }

    [Fact]
    public async Task AsignarManual_SinCandidatos_ConUnCandidatoValido_YAuditada()
    {
        var (conductor, vehiculo) = ConductorConVehiculo();
        var solicitud = Solicitud();
        solicitud.MarcarSinCandidatos("Sin conductores", Ahora);
        var casoUso = new AsignarManualCasoUso(_solicitudes, Asignaciones, Validador, Auditoria, _bandeja, UnidadTrabajo, Reloj);

        var asignacion = await casoUso.EjecutarAsync(new AsignarManualDominio(solicitud.Id, conductor.Id, vehiculo.Id, "Contingencia", Actor),
            CancellationToken.None);

        Assert.Equal((OrigenAsignacion.Manual, EstadoSolicitud.Asignada), (asignacion.Origen, solicitud.Estado));
        Assert.Equal(OrigenAsignacion.Manual, Assert.Single(_decisiones.Decisiones).Origen);
        Assert.Equal(("asignacion.asignar_manual", "Contingencia"), (Auditoria.Eventos[^1].Accion, Auditoria.Eventos[^1].Motivo));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AsignarManualDominio(solicitud.Id, conductor.Id, vehiculo.Id, "Otra", Actor), CancellationToken.None));
    }

    [Fact]
    public async Task AsignarManual_UnCandidatoQueNoCumple_SeRechazaConElMotivo()
    {
        var (conductor, _) = ConductorConVehiculo();
        var pequeno = new Vehiculo(Guid.NewGuid(), "PEQ01", "Kia", "Picanto", 1, Vigente, Vigente);
        Vehiculos.Elementos[pequeno.Id] = pequeno;
        var solicitud = Solicitud(pasajeros: 3);
        var casoUso = new AsignarManualCasoUso(_solicitudes, Asignaciones, Validador, Auditoria, _bandeja, UnidadTrabajo, Reloj);

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AsignarManualDominio(solicitud.Id, conductor.Id, pequeno.Id, "Contingencia", Actor), CancellationToken.None));

        Assert.Contains("Capacidad", error.Message, StringComparison.Ordinal);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AsignarManualDominio(solicitud.Id, conductor.Id, pequeno.Id, " ", Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Reasignar_ReemplazaLaAsignacion_ConOtroConductor()
    {
        ConductorConVehiculo("1001");
        var solicitud = Solicitud();
        var inicial = await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);
        var (otro, suVehiculo) = ConductorConVehiculo("2002");
        var casoUso = new ReasignarCasoUso(Asignaciones, Viajes, _solicitudes, Validador, Auditoria, _bandeja, UnidadTrabajo, Reloj);

        var nueva = await casoUso.EjecutarAsync(new ReasignarDominio(inicial.AsignacionId!.Value, otro.Id, suVehiculo.Id, "Conductor enfermo", Actor),
            CancellationToken.None);

        Assert.Equal(EstadoAsignacion.Reemplazada, Asignaciones.Todas[inicial.AsignacionId.Value].Estado);
        Assert.Equal((otro.Id, OrigenAsignacion.Manual, inicial.AsignacionId), (nueva.ConductorId, nueva.Origen, nueva.AsignacionReemplazadaId));
        Assert.Equal(EstadoSolicitud.Asignada, solicitud.Estado);
        Assert.Equal("asignacion.reasignar", Auditoria.Eventos[^1].Accion);
        Assert.Equal(EventosSalida.AsignacionReasignada, _bandeja.Eventos[^1].Tipo);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ReasignarDominio(nueva.Id, otro.Id, suVehiculo.Id, "Igual", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ReasignarDominio(inicial.AsignacionId.Value, otro.Id, suVehiculo.Id, "Ya reemplazada", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ReasignarDominio(Guid.NewGuid(), otro.Id, suVehiculo.Id, "No existe", Actor), CancellationToken.None));
    }

    [Fact]
    public async Task DevolverACola_SoloSinCandidatos()
    {
        var solicitud = Solicitud();
        var casoUso = new DevolverAColaCasoUso(_solicitudes, Auditoria, UnidadTrabajo, Reloj);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            casoUso.EjecutarAsync(new DevolverAColaDominio(solicitud.Id, "Reintentar", Actor), CancellationToken.None));

        solicitud.MarcarSinCandidatos("Sin conductores", Ahora);
        await casoUso.EjecutarAsync(new DevolverAColaDominio(solicitud.Id, "Llegó un conductor", Actor), CancellationToken.None);

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal("solicitud.devolver_a_cola", Auditoria.Eventos[^1].Accion);
    }

    [Fact]
    public async Task AbandonarTurno_CancelaSusAsignacionesYDevuelveLasSolicitudesALaCola()
    {
        var (conductor, _) = ConductorConVehiculo();
        var solicitud = Solicitud();
        await Asignar().EjecutarAsync(new AsignarSolicitudDominio(solicitud.Id), CancellationToken.None);
        var casoUso = new AbandonarTurnoCasoUso(ConductorDominio, Conductores, Asignaciones, Viajes, _solicitudes, Auditoria, _bandeja, UnidadTrabajo, Reloj);

        var devueltas = await casoUso.EjecutarAsync(new AbandonarTurnoDominio(conductor.Id, conductor.Turnos[0].Id, "Emergencia familiar", Actor),
            CancellationToken.None);

        Assert.Equal([solicitud.Id], devueltas);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal(EstadoAsignacion.Cancelada, Assert.Single(Asignaciones.Todas.Values).Estado);
        Assert.Equal(EstadoTurno.Abandonado, conductor.Turnos[0].Estado);
        Assert.Equal(EventosSalida.TurnoAbandonado, _bandeja.Eventos[^1].Tipo);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AbandonarTurnoDominio(conductor.Id, conductor.Turnos[0].Id, "Otra vez", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AbandonarTurnoDominio(conductor.Id, Guid.NewGuid(), "No existe", Actor), CancellationToken.None));
    }
}

public class ConfiguracionMotorCasosUsoTests : CasosUsoOperacionBase
{
    private static readonly OperadorDelStack Operador = new("tenant-a");
    private static readonly CatalogoReglas Catalogo = new(DefaultAssignmentRules.Create());

    private readonly ConfiguracionesEnMemoria _configuraciones = new ConfiguracionesEnMemoria().Con(ConfiguracionesEnMemoria.Nueva(1, activa: true));

    private CrearConfiguracionCasoUso Crear() => new(_configuraciones, Catalogo, Operador, Auditoria, UnidadTrabajo, Reloj);
    private ActivarConfiguracionCasoUso Activar() => new(_configuraciones, Catalogo, Operador, Auditoria, UnidadTrabajo, Reloj);

    [Fact]
    public async Task CrearActivarYRollback()
    {
        var reglas = ConfiguracionInicial.Reglas.Select(r => r.Codigo == "WorkloadBalanceRule" ? r with { Peso = 0.4m } : r).ToList();

        var v2 = await Crear().EjecutarAsync(new CrearConfiguracionDominio(reglas, Actor), CancellationToken.None);
        Assert.Equal((2, EstadoConfiguracion.Borrador), (v2.Version, v2.Estado));

        await Activar().EjecutarAsync(new ActivarConfiguracionDominio(2, Actor), CancellationToken.None);
        var v1 = _configuraciones.Configuraciones.Values.Single(c => c.Version == 1);
        Assert.Equal((EstadoConfiguracion.Archivada, EstadoConfiguracion.Activa), (v1.Estado, v2.Estado));

        await Activar().EjecutarAsync(new ActivarConfiguracionDominio(1, Actor), CancellationToken.None);
        Assert.Equal((EstadoConfiguracion.Activa, EstadoConfiguracion.Archivada), (v1.Estado, v2.Estado));
        await Activar().EjecutarAsync(new ActivarConfiguracionDominio(1, Actor), CancellationToken.None);
        Assert.Equal(["configuracion_motor.crear", "configuracion_motor.activar", "configuracion_motor.activar"], Auditoria.Eventos.Select(e => e.Accion));

        var todas = await new ConsultarConfiguracionesCasoUso(_configuraciones).EjecutarAsync(ConsultarConfiguracionesDominio.Todas, CancellationToken.None);
        Assert.Equal([2, 1], todas.Select(c => c.Version));
    }

    [Fact]
    public async Task UnaConfiguracionInvalida_NoSeCreaNiSeActiva()
    {
        var sinObligatoria = ConfiguracionInicial.Reglas.Select(r => r.Codigo == "ShiftCompatibilityRule" ? r with { Habilitada = false } : r).ToList();

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Crear().EjecutarAsync(new CrearConfiguracionDominio(sinObligatoria, Actor), CancellationToken.None));

        Assert.Contains("obligatoria 'ShiftCompatibilityRule'", error.Message, StringComparison.Ordinal);
        Assert.Single(_configuraciones.Configuraciones);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Activar().EjecutarAsync(new ActivarConfiguracionDominio(9, Actor), CancellationToken.None));
        _configuraciones.Con(ConfiguracionesEnMemoria.Nueva(3, sinObligatoria));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Activar().EjecutarAsync(new ActivarConfiguracionDominio(3, Actor), CancellationToken.None));
    }

    private sealed class ConsultasFalsas : IConsultasAsignaciones
    {
        public Task<PaginaResultado<AsignacionLectura>> BuscarAsync(FiltroAsignaciones filtro, Paginacion paginacion, CancellationToken ct) =>
            Task.FromResult(new PaginaResultado<AsignacionLectura>([], 0, paginacion));

        public Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DecisionLectura>>([]);
    }

    [Fact]
    public async Task LasConsultasDelegan()
    {
        Assert.Empty(await new ConsultarDecisionesCasoUso(new ConsultasFalsas()).EjecutarAsync(new ConsultarDecisionesDominio(Guid.NewGuid()), default));
        Assert.Equal(0, (await new BuscarAsignacionesCasoUso(new ConsultasFalsas()).EjecutarAsync(
            new BuscarAsignacionesDominio(new FiltroAsignaciones(), Paginacion.PorDefecto), default)).Total);
    }
}
