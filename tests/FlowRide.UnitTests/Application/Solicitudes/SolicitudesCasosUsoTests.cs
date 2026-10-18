using FlowRide.Application.Solicitudes.CasosUso;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.UnitTests.Application.Operacion;

namespace FlowRide.UnitTests.Application.Solicitudes;

internal sealed class SolicitudesEnMemoria(PasajerosEnMemoria pasajeros, EmpresasEnMemoria empresas)
    : ISolicitudRepositorio, IConsultasSolicitudes
{
    public Dictionary<Guid, SolicitudViaje> Solicitudes { get; } = [];

    public Task CrearAsync(SolicitudViaje entidad, CancellationToken ct)
    {
        Solicitudes.Add(entidad.Id, entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, SolicitudViaje entidad, CancellationToken ct) => Task.CompletedTask;

    public Task<SolicitudViaje?> ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Solicitudes.GetValueOrDefault(id));

    public Task<IReadOnlyList<SolicitudViaje>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SolicitudViaje>>(Solicitudes.Values.ToList());

    public Task<bool> ExisteActivaQueSeSolapaAsync(Guid pasajeroId, FranjaHoraria franja, Guid? excluirSolicitudId, CancellationToken ct) =>
        Task.FromResult(Solicitudes.Values.Any(s => s.PasajeroId == pasajeroId && s.Id != excluirSolicitudId && s.EstaActiva
                                                    && s.Franja().SeSolapaCon(franja)));

    public Task<IReadOnlyList<Guid>> ConsultarIdsPendientesAsync(int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Solicitudes.Values.Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .OrderByDescending(s => s.Urgente).ThenBy(s => s.FechaHoraServicio).Select(s => s.Id).Take(maximo).ToList());

    public Task<SolicitudDetalleLectura?> DetalleAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult(Solicitudes.GetValueOrDefault(solicitudId) is { } s
            ? new SolicitudDetalleLectura(Lectura(s), s.Historial.Select(h =>
                new CambioEstadoLectura(h.EstadoAnterior, h.EstadoNuevo, h.FechaHora, h.ActorId, h.Motivo)).ToList())
            : null);

    public Task<PaginaResultado<SolicitudLectura>> BuscarAsync(FiltroSolicitudes filtro, Paginacion paginacion, CancellationToken ct)
    {
        var lista = Solicitudes.Values.Where(s => filtro.Estado is null || s.Estado == filtro.Estado).Select(Lectura).ToList();
        return Task.FromResult(new PaginaResultado<SolicitudLectura>(lista, lista.Count, paginacion));
    }

    private SolicitudLectura Lectura(SolicitudViaje s) => new(s.Id, s.PasajeroId, pasajeros.Elementos[s.PasajeroId].Nombre,
        s.EmpresaClienteId, s.EmpresaClienteId is { } e ? empresas.Elementos[e].Nombre : null, s.ItinerarioId, s.DireccionOrigen,
        s.DireccionDestino, s.FechaHoraServicio, s.DuracionEstimada, s.CantidadPasajeros, s.Urgente, s.EsPersonal, s.Canal, s.Estado,
        s.RegistradaEn);
}

internal sealed class ItinerariosEnMemoria : IItinerarioRepositorio
{
    public Dictionary<Guid, Itinerario> Itinerarios { get; } = [];

    public Task CrearAsync(Itinerario entidad, CancellationToken ct)
    {
        Itinerarios.Add(entidad.Id, entidad);
        return Task.CompletedTask;
    }

    public Task<Itinerario?> ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Itinerarios.GetValueOrDefault(id));

    public Task<IReadOnlyList<Itinerario>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Itinerario>>(Itinerarios.Values.ToList());

    public Task<IReadOnlySet<DateOnly>> ConsultarFechasOcupadasAsync(
        Guid empresaClienteId, string codigoServicio, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<DateOnly>>(Itinerarios.Values
            .Where(i => i.EmpresaClienteId == empresaClienteId && i.CodigoServicio == codigoServicio && i.Fecha >= desde && i.Fecha <= hasta)
            .Select(i => i.Fecha).ToHashSet());

    public Task<PaginaResultado<Itinerario>> BuscarAsync(FiltroItinerarios filtro, Paginacion paginacion, CancellationToken ct)
    {
        var lista = Itinerarios.Values.Where(i => filtro.EmpresaClienteId is null || i.EmpresaClienteId == filtro.EmpresaClienteId).ToList();
        return Task.FromResult(new PaginaResultado<Itinerario>(lista, lista.Count, paginacion));
    }
}

public class SolicitudesCasosUsoTests : CasosUsoOperacionBase
{
    private static readonly ZonaHorariaOperador Zona = new(TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));
    private static readonly DateTimeOffset Servicio = Ahora.AddDays(1);

    private readonly ParametrosEnMemoria _parametros = new();
    private readonly ItinerariosEnMemoria _itinerarios = new();
    private readonly SolicitudesEnMemoria _solicitudes;
    private readonly EmpresaCliente _empresa;
    private readonly Pasajero _pasajero;

    public SolicitudesCasosUsoTests()
    {
        _solicitudes = new SolicitudesEnMemoria(Pasajeros, Empresas);
        _empresa = Empresas.Registrar();
        _pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", _empresa.Id);
        Pasajeros.Elementos[_pasajero.Id] = _pasajero;
    }

    private ParametrosDominio Parametros => new(_parametros);
    private SolicitudDominio SolicitudDominio => new(_solicitudes, Parametros);

    private RegistrarSolicitudCasoUso Registrar() => new(new PasajeroDominio(Pasajeros), new EmpresaClienteDominio(Empresas),
        SolicitudDominio, _itinerarios, _solicitudes, Zona, Auditoria, UnidadTrabajo, Reloj);

    private RegistrarSolicitudDominio Datos(DateTimeOffset? servicio = null, Guid? itinerarioId = null, bool personal = false) =>
        new(_pasajero.Id, itinerarioId, itinerarioId is null ? "Hotel" : null, itinerarioId is null ? "Aeropuerto" : null,
            itinerarioId is null ? servicio ?? Servicio : null, itinerarioId is null ? TimeSpan.FromMinutes(45) : null, 2, false, personal,
            CanalSolicitud.PanelWeb, Actor);

    private CancelarSolicitudCasoUso Cancelar() => new(SolicitudDominio, _solicitudes, Asignaciones, Auditoria, UnidadTrabajo, Reloj);

    [Fact]
    public async Task Registrar_QuedaPendienteEnLaCola_YSeAudita()
    {
        var solicitud = await Registrar().EjecutarAsync(Datos(), CancellationToken.None);

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal(_empresa.Id, solicitud.EmpresaClienteId);
        Assert.Equal(Actor, solicitud.Historial[0].ActorId);
        Assert.Equal("solicitud.crear", UltimaAccion);
        Assert.DoesNotContain("Hotel", Auditoria.Eventos[0].ValoresDespues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registrar_DesdeUnItinerarioTomaSusDatosEnLaHoraLocal()
    {
        var itinerario = new Itinerario(Guid.NewGuid(), _empresa.Id, "S-1", DateOnly.FromDateTime(Ahora.AddDays(2).Date), "Base",
            "Mina", new TimeOnly(6, 0), new TimeOnly(7, 30));
        _itinerarios.Itinerarios[itinerario.Id] = itinerario;

        var solicitud = await Registrar().EjecutarAsync(Datos(itinerarioId: itinerario.Id), CancellationToken.None);

        Assert.Equal(("Base", "Mina"), (solicitud.DireccionOrigen, solicitud.DireccionDestino));
        Assert.Equal(Zona.Instante(itinerario.Fecha, itinerario.HoraSalida), solicitud.FechaHoraServicio);
        Assert.Equal(TimeSpan.FromMinutes(90), solicitud.DuracionEstimada);
        Assert.Equal(itinerario.Id, solicitud.ItinerarioId);
    }

    [Fact]
    public async Task Registrar_RechazaDatosIncompletosPasadosSolapadosOItinerarioAjeno()
    {
        var ajeno = new Itinerario(Guid.NewGuid(), Guid.NewGuid(), "S-9", DateOnly.FromDateTime(Ahora.AddDays(2).Date), "A", "B",
            new TimeOnly(6, 0), new TimeOnly(7, 0));
        _itinerarios.Itinerarios[ajeno.Id] = ajeno;
        await Registrar().EjecutarAsync(Datos(), CancellationToken.None);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Datos(), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Datos(Ahora.AddHours(-1)), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Datos(itinerarioId: ajeno.Id), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Datos(itinerarioId: Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(
            Datos() with { DireccionOrigen = null, FechaHoraServicio = Servicio.AddDays(3) }, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Datos() with { PasajeroId = Guid.NewGuid() }, CancellationToken.None));
        Assert.Single(_solicitudes.Solicitudes);
    }

    [Fact]
    public async Task Modificar_DentroDelPlazoYSinSolaparse()
    {
        var solicitud = await Registrar().EjecutarAsync(Datos(), CancellationToken.None);
        var modificar = new ModificarSolicitudCasoUso(SolicitudDominio, _solicitudes, Auditoria, UnidadTrabajo, Reloj);

        await modificar.EjecutarAsync(new ModificarSolicitudDominio(solicitud.Id, "Casa", "Oficina", Servicio.AddHours(1),
            TimeSpan.FromMinutes(30), 1, true, Actor), CancellationToken.None);

        Assert.Equal(("Casa", true), (solicitud.DireccionOrigen, solicitud.Urgente));
        Assert.Equal("solicitud.modificar", UltimaAccion);
    }

    [Fact]
    public async Task Modificar_FueraDelPlazo_SeRechazaParaTodos()
    {
        var solicitud = await Registrar().EjecutarAsync(Datos(Ahora.AddMinutes(20)), CancellationToken.None);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ModificarSolicitudCasoUso(SolicitudDominio, _solicitudes, Auditoria,
                UnidadTrabajo, Reloj)
            .EjecutarAsync(new ModificarSolicitudDominio(solicitud.Id, "A", "B", Servicio, TimeSpan.FromMinutes(30), 1, false, Actor),
                CancellationToken.None));
    }

    [Fact]
    public async Task Cancelar_Pendiente_GuardaElMotivo()
    {
        var solicitud = await Registrar().EjecutarAsync(Datos(), CancellationToken.None);

        await Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Ya no viaja", Actor, false), CancellationToken.None);

        Assert.Equal(EstadoSolicitud.Cancelada, solicitud.Estado);
        Assert.Equal("Ya no viaja", solicitud.Cancelacion!.Motivo);
        Assert.Equal(("solicitud.cancelar", "Ya no viaja"), (Auditoria.Eventos[^1].Accion, Auditoria.Eventos[^1].Motivo));
    }

    [Fact]
    public async Task Cancelar_AsignadaCancelaTambienSuAsignacion()
    {
        var solicitud = await Registrar().EjecutarAsync(Datos(), CancellationToken.None);
        var asignacion = new Asignacion(Guid.NewGuid(), solicitud.Id, Guid.NewGuid(), Guid.NewGuid(), solicitud.Franja(),
            OrigenAsignacion.Automatica, Ahora, decisionId: Guid.NewGuid());
        solicitud.MarcarAsignada(asignacion, null, Ahora);
        Asignaciones.VigentesPorSolicitud[solicitud.Id] = asignacion;

        await Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Cambio de turno", Actor, false), CancellationToken.None);

        Assert.Equal(EstadoAsignacion.Cancelada, asignacion.Estado);
    }

    [Fact]
    public async Task Cancelar_FueraDelPlazo_SoloElOperadorLogistico()
    {
        _parametros.Parametros[CatalogoParametros.VentanaCancelacionMinutos] =
            new Parametro(CatalogoParametros.Buscar(CatalogoParametros.VentanaCancelacionMinutos), "60", null, Ahora);
        var solicitud = await Registrar().EjecutarAsync(Datos(Ahora.AddMinutes(45)), CancellationToken.None);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Tarde", Actor, false), CancellationToken.None));
        await Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Contingencia", Actor, true), CancellationToken.None);

        Assert.Equal(EstadoSolicitud.Cancelada, solicitud.Estado);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, "Otra vez", Actor, true), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Cancelar().EjecutarAsync(new CancelarSolicitudDominio(solicitud.Id, " ", Actor, true), CancellationToken.None));
    }

    [Fact]
    public async Task Consultar_OcultaLasDireccionesDeLasPersonalesSegunElLector()
    {
        var personal = await Registrar().EjecutarAsync(Datos(personal: true), CancellationToken.None);
        var consultar = new ConsultarSolicitudCasoUso(_solicitudes, Parametros);

        var paraOperador = await consultar.EjecutarAsync(new ConsultarSolicitudDominio(personal.Id, LectorSolicitud.OperadorLogistico), CancellationToken.None);
        var paraAdministrador = await consultar.EjecutarAsync(new ConsultarSolicitudDominio(personal.Id, LectorSolicitud.Administrador), CancellationToken.None);
        var lista = await new BuscarSolicitudesCasoUso(_solicitudes, Parametros).EjecutarAsync(
            new BuscarSolicitudesDominio(new FiltroSolicitudes(), Paginacion.PorDefecto, LectorSolicitud.Administrador), CancellationToken.None);

        Assert.Equal("Hotel", paraOperador.Solicitud.DireccionOrigen);
        Assert.Single(paraOperador.Historial);
        Assert.True(paraAdministrador.Solicitud.DetalleRestringido);
        Assert.Null(paraAdministrador.Solicitud.DireccionOrigen);
        Assert.Null(Assert.Single(lista.Elementos).DireccionDestino);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            consultar.EjecutarAsync(new ConsultarSolicitudDominio(Guid.NewGuid(), LectorSolicitud.Administrador), CancellationToken.None));
    }

    [Fact]
    public async Task Itinerarios_RangoConDias_YSinRepetirFechas()
    {
        var registrar = new RegistrarItinerariosCasoUso(new EmpresaClienteDominio(Empresas), _itinerarios, Auditoria, UnidadTrabajo, Reloj);
        var datos = new RegistrarItinerariosDominio(_empresa.Id, "S-1", "Base", "Mina", new TimeOnly(6, 0), new TimeOnly(7, 0),
            new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30), [DayOfWeek.Monday, DayOfWeek.Friday], Actor);

        var creados = await registrar.EjecutarAsync(datos, CancellationToken.None);

        Assert.Equal(4, creados.Count);
        Assert.Equal(4, Auditoria.Eventos.Count(e => e.Accion == "itinerario.crear"));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => registrar.EjecutarAsync(datos with { FechaDesde = new DateOnly(2026, 10, 30) }, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => registrar.EjecutarAsync(datos with { CodigoServicio = "S-2", HoraLlegada = new TimeOnly(5, 0) }, CancellationToken.None));
        var empresaInactiva = Empresas.Registrar(activa: false);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => registrar.EjecutarAsync(datos with { EmpresaClienteId = empresaInactiva.Id }, CancellationToken.None));
        Assert.Equal(4, (await new BuscarItinerariosCasoUso(_itinerarios).EjecutarAsync(
            new BuscarItinerariosDominio(new FiltroItinerarios(_empresa.Id), Paginacion.PorDefecto), CancellationToken.None)).Total);
    }
}
