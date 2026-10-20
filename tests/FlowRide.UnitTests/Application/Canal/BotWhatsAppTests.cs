using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Canal.Bot;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.GestionCanal;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.UnitTests.Application.Canal;

internal sealed class Grabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder) : ICasoUsoConRetorno<TEntrada, TSalida>
{
    public List<TEntrada> Recibidos { get; } = [];
    public Exception? Falla { get; set; }

    public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
    {
        Recibidos.Add(datos);
        return Falla is null ? Task.FromResult(responder(datos)) : Task.FromException<TSalida>(Falla);
    }
}

/// <summary>Lecturas en memoria para el bot: solicitudes, asignaciones y viajes, filtrados como lo hacen las consultas reales.</summary>
internal sealed class LecturasEnMemoria : IConsultasSolicitudes, IConsultasAsignaciones, IConsultasViajes
{
    public List<SolicitudLectura> Solicitudes { get; } = [];
    public List<AsignacionLectura> Asignaciones { get; } = [];
    public List<ViajeLectura> Viajes { get; } = [];

    public Task<SolicitudDetalleLectura?> DetalleAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult(Solicitudes.FirstOrDefault(s => s.Id == solicitudId) is { } s ? new SolicitudDetalleLectura(s, []) : null);

    public Task<PaginaResultado<SolicitudLectura>> BuscarAsync(FiltroSolicitudes filtro, Paginacion paginacion, CancellationToken ct) =>
        Pagina(Solicitudes.Where(s => (filtro.PasajeroId is null || s.PasajeroId == filtro.PasajeroId) && (filtro.Estado is null || s.Estado == filtro.Estado)),
            paginacion);

    public Task<PaginaResultado<AsignacionLectura>> BuscarAsync(FiltroAsignaciones filtro, Paginacion paginacion, CancellationToken ct) =>
        Pagina(Asignaciones.Where(a => (filtro.ConductorId is null || a.ConductorId == filtro.ConductorId)
                                       && (filtro.SolicitudId is null || a.SolicitudId == filtro.SolicitudId)
                                       && (filtro.Estado is null || a.Estado == filtro.Estado)), paginacion);

    public Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct) => Task.FromResult<IReadOnlyList<DecisionLectura>>([]);

    public Task<PaginaResultado<ViajeLectura>> BuscarAsync(FiltroViajes filtro, Paginacion paginacion, CancellationToken ct) =>
        Pagina(Viajes.Where(v => (filtro.Estado is null || v.Estado == filtro.Estado) && (filtro.PasajeroId is null || v.PasajeroId == filtro.PasajeroId)
                                 && (filtro.ConductorId is null || v.ConductorId == filtro.ConductorId)), paginacion);

    Task<ViajeDetalleLectura?> IConsultasViajes.DetalleAsync(Guid viajeId, CancellationToken ct) => Task.FromResult<ViajeDetalleLectura?>(null);

    public Task<IReadOnlyList<ViajeLectura>> ActivosAsync(DateTimeOffset programadosHasta, CancellationToken ct) => Task.FromResult<IReadOnlyList<ViajeLectura>>([]);

    public Task<IReadOnlyList<ViajeLectura>> ListarAsync(FiltroViajes filtro, int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ViajeLectura>>([]);

    public Task<TrazabilidadLectura?> TrazabilidadAsync(Guid solicitudId, CancellationToken ct) => Task.FromResult<TrazabilidadLectura?>(null);

    private static Task<PaginaResultado<T>> Pagina<T>(IEnumerable<T> elementos, Paginacion paginacion)
    {
        var lista = elementos.ToList();
        return Task.FromResult(new PaginaResultado<T>(lista.Take(paginacion.Tamano).ToList(), lista.Count, paginacion));
    }
}

/// <summary>El bot con sus dependencias en memoria y grabadores en lugar de los casos de uso de negocio.</summary>
internal sealed class BotDePrueba
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 18, 13, 0, 0, TimeSpan.Zero);
    public static readonly ZonaHorariaOperador Zona = new(TimeZoneInfo.Utc);

    public LecturasEnMemoria Lecturas { get; } = new();
    public Grabador<RegistrarSolicitudDominio, SolicitudViaje> Registrar { get; } = new(d => new SolicitudViaje(Guid.NewGuid(), d.PasajeroId,
        d.DireccionOrigen!, d.DireccionDestino!, d.FechaHoraServicio!.Value, d.DuracionEstimada!.Value, d.CantidadPasajeros, d.Canal, Ahora));
    public Grabador<CancelarSolicitudDominio, SolicitudViaje> Cancelar { get; } = new(_ => null!);
    public Grabador<CalificarViajeDominio, Calificacion> Calificar { get; } =
        new(d => new Calificacion(Guid.NewGuid(), d.ViajeId, d.ActorId, d.Puntaje, d.Comentario, Ahora));
    public Grabador<AceptarAsignacionDominio, Viaje> Aceptar { get; } = new(d => new Viaje(Guid.NewGuid(), d.AsignacionId));
    public Grabador<RechazarAsignacionDominio, Asignacion> Rechazar { get; } = new(_ => null!);
    public Grabador<IniciarViajeDominio, Viaje> Iniciar { get; } = new(d => new Viaje(d.ViajeId, Guid.NewGuid()));
    public Grabador<FinalizarViajeDominio, Viaje> Finalizar { get; } = new(d => new Viaje(d.ViajeId, Guid.NewGuid()));
    public Grabador<ReportarIncidenteDominio, Incidente> Reportar { get; } =
        new(d => new Incidente(Guid.NewGuid(), d.ViajeId, d.Tipo, d.Descripcion, Ahora, d.ActorId));

    public BotWhatsApp Bot => new(Zona, Lecturas, Lecturas, Lecturas, Registrar, Cancelar, Calificar, Aceptar, Rechazar, Iniciar, Finalizar, Reportar);
}

public class BotWhatsAppTests
{
    private static readonly DateTimeOffset Ahora = BotDePrueba.Ahora;
    private readonly BotDePrueba _prueba = new();
    private readonly Pasajero _pasajero = new(Guid.NewGuid(), "Ana María Ruiz", "+573001112233", RolesSistema.PasajeroId, Ahora, "E-1", Guid.NewGuid());
    private readonly Conductor _conductor = new(Guid.NewGuid(), "Luis Pérez", "+573004445566", RolesSistema.ConductorId, Ahora, "LIC", "C2",
        new DateOnly(2028, 1, 1));
    private readonly Conversacion _conversacion = new(Guid.NewGuid(), "+573001112233", Ahora);

    private async Task<string> EscribirAsync(Usuario usuario, string texto, string tipo = MensajeRecibido.TipoTexto, DateTimeOffset? cuando = null)
    {
        var decision = await _prueba.Bot.DecidirAsync(_conversacion, usuario, tipo, texto, cuando ?? Ahora, CancellationToken.None);
        return decision.Accion is null ? decision.Respuesta : await decision.Accion(CancellationToken.None);
    }

    private SolicitudLectura Solicitud(EstadoSolicitud estado, int horas = 24, Guid? pasajeroId = null) =>
        new(Guid.NewGuid(), pasajeroId ?? _pasajero.Id, "Ana", null, null, null, "Hotel Centro", "Aeropuerto", Ahora.AddHours(horas),
            TimeSpan.FromMinutes(45), 2, false, false, CanalSolicitud.WhatsApp, estado, Ahora);

    private ViajeLectura Viaje(EstadoViaje estado, Guid? conductorId = null, Guid? pasajeroId = null, int? puntaje = null, int horas = 2) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), estado, conductorId ?? _conductor.Id, "Luis", Guid.NewGuid(), "ABC123",
            pasajeroId ?? _pasajero.Id, "Ana", null, null, "Hotel Centro", "Aeropuerto", false, false, Ahora.AddHours(horas), Ahora.AddHours(horas + 1),
            null, null, 0, puntaje);

    [Fact]
    public async Task Pasajero_PideTransporte_PasoAPaso()
    {
        Assert.Contains("1. Pedir transporte", await EscribirAsync(_pasajero, "hola"));
        Assert.StartsWith("Hola, Ana.", await EscribirAsync(_pasajero, "menu"));
        Assert.Contains("recogemos", await EscribirAsync(_pasajero, "1"));
        Assert.Contains("A dónde", await EscribirAsync(_pasajero, "Hotel Centro"));
        Assert.Contains("Para cuándo", await EscribirAsync(_pasajero, "Aeropuerto JMC"));
        Assert.Contains("Cuántas personas", await EscribirAsync(_pasajero, "mañana 7:30"));
        var resumen = await EscribirAsync(_pasajero, "3");
        Assert.Contains("Recogida: Hotel Centro", resumen);
        Assert.Contains("19/10 07:30", resumen);

        var confirmacion = await EscribirAsync(_pasajero, "1");

        Assert.StartsWith("Listo, registramos tu solicitud para el 19/10 07:30", confirmacion);
        var datos = Assert.Single(_prueba.Registrar.Recibidos);
        Assert.Equal((_pasajero.Id, "Hotel Centro", "Aeropuerto JMC", new DateTimeOffset(2026, 10, 19, 7, 30, 0, TimeSpan.Zero), 3, CanalSolicitud.WhatsApp,
                BotWhatsApp.DuracionPorDefecto, _pasajero.Id),
            (datos.PasajeroId, datos.DireccionOrigen, datos.DireccionDestino, datos.FechaHoraServicio, datos.CantidadPasajeros, datos.Canal,
                datos.DuracionEstimada, datos.ActorId));
        Assert.Equal(PasoConversacion.Menu, _conversacion.Paso);
    }

    [Fact]
    public async Task Pasajero_DatosInvalidos_RepitenElPaso_YDescartarNoRegistra()
    {
        await EscribirAsync(_pasajero, "1");
        Assert.Contains("al menos 3", await EscribirAsync(_pasajero, "a"));
        await EscribirAsync(_pasajero, "Hotel Centro");
        await EscribirAsync(_pasajero, "Aeropuerto");
        Assert.Contains("No entendí la fecha", await EscribirAsync(_pasajero, "el lunes"));
        Assert.Contains("ya pasó", await EscribirAsync(_pasajero, "hoy 8:00"));
        await EscribirAsync(_pasajero, "hoy 18:00");
        Assert.Contains("de 1 a 20", await EscribirAsync(_pasajero, "40"));
        await EscribirAsync(_pasajero, "2");
        Assert.Contains("1 para confirmar", await EscribirAsync(_pasajero, "sí"));

        Assert.Contains("Descartamos", await EscribirAsync(_pasajero, "2"));
        Assert.Empty(_prueba.Registrar.Recibidos);
    }

    [Fact]
    public async Task Pasajero_VeSusSolicitudes_ConConductorYPlaca()
    {
        var asignada = Solicitud(EstadoSolicitud.Asignada);
        _prueba.Lecturas.Solicitudes.AddRange([asignada, Solicitud(EstadoSolicitud.Pendiente, 48), Solicitud(EstadoSolicitud.Pendiente, 5, Guid.NewGuid())]);
        _prueba.Lecturas.Asignaciones.Add(new AsignacionLectura(Guid.NewGuid(), asignada.Id, _conductor.Id, "Luis Pérez", Guid.NewGuid(), "ABC123",
            asignada.FechaHoraServicio, asignada.FechaHoraServicio.AddHours(1), EstadoAsignacion.Aceptada, OrigenAsignacion.Automatica, Ahora, null, null));

        var respuesta = await EscribirAsync(_pasajero, "2");

        Assert.Contains("con conductor asignado (Luis Pérez, placa ABC123)", respuesta);
        Assert.Contains("buscando conductor", respuesta);
        Assert.Equal(2, respuesta.Split('•').Length - 1);
        Assert.Contains("No tienes solicitudes", await new BotWhatsAppTests().EscribirAsync(new Pasajero(Guid.NewGuid(), "Otro", "+57", RolesSistema.PasajeroId,
            Ahora, "E-2", Guid.NewGuid()), "2"));
    }

    [Fact]
    public async Task Pasajero_CancelaConMotivo_SoloSusSolicitudesCancelables()
    {
        var cancelable = Solicitud(EstadoSolicitud.Pendiente);
        _prueba.Lecturas.Solicitudes.AddRange([cancelable, Solicitud(EstadoSolicitud.Completada)]);

        var lista = await EscribirAsync(_pasajero, "3");
        Assert.Contains("1. ", lista);
        Assert.DoesNotContain("2. ", lista);
        Assert.Equal(TextosBot.OpcionInvalida, await EscribirAsync(_pasajero, "2"));
        Assert.Contains("motivo", await EscribirAsync(_pasajero, "1"));
        Assert.Contains("Cancelamos", await EscribirAsync(_pasajero, "Cambió mi vuelo"));

        Assert.Equal(new CancelarSolicitudDominio(cancelable.Id, "Cambió mi vuelo", _pasajero.Id, false), Assert.Single(_prueba.Cancelar.Recibidos));
    }

    [Fact]
    public async Task Pasajero_CalificaUnViajeSinCalificar()
    {
        var sinCalificar = Viaje(EstadoViaje.Finalizado);
        _prueba.Lecturas.Viajes.AddRange([sinCalificar, Viaje(EstadoViaje.Finalizado, puntaje: 5)]);

        await EscribirAsync(_pasajero, "4");
        Assert.Contains("Del 1 al 5", await EscribirAsync(_pasajero, "1"));
        Assert.Contains("número del 1 al 5", await EscribirAsync(_pasajero, "9"));
        await EscribirAsync(_pasajero, "4");
        Assert.Contains("Gracias", await EscribirAsync(_pasajero, "0"));

        Assert.Equal(new CalificarViajeDominio(sinCalificar.Id, 4, null, _pasajero.Id), Assert.Single(_prueba.Calificar.Recibidos));
    }

    [Fact]
    public async Task Pasajero_SinOpciones_VuelveAlMenu()
    {
        Assert.Contains("No tienes solicitudes que puedas cancelar", await EscribirAsync(_pasajero, "3"));
        Assert.Contains("No tienes viajes terminados por calificar", await EscribirAsync(_pasajero, "4"));
        Assert.Equal(PasoConversacion.Menu, _conversacion.Paso);
    }

    [Fact]
    public async Task Conductor_AceptaYRechazaSusAsignaciones()
    {
        var solicitud = Solicitud(EstadoSolicitud.Asignada);
        _prueba.Lecturas.Solicitudes.Add(solicitud);
        var primera = new AsignacionLectura(Guid.NewGuid(), solicitud.Id, _conductor.Id, "Luis", Guid.NewGuid(), "ABC123", solicitud.FechaHoraServicio,
            solicitud.FechaHoraServicio.AddHours(1), EstadoAsignacion.Asignada, OrigenAsignacion.Automatica, Ahora, null, null);
        _prueba.Lecturas.Asignaciones.AddRange([primera,
            primera with { Id = Guid.NewGuid(), ConductorId = Guid.NewGuid() }, primera with { Id = Guid.NewGuid(), Estado = EstadoAsignacion.Aceptada }]);

        Assert.Contains("1. Responder asignaciones", await EscribirAsync(_conductor, "menu"));
        var lista = await EscribirAsync(_conductor, "1");
        Assert.Contains("Hotel Centro → Aeropuerto · 2 pasajero(s) · placa ABC123", lista);
        Assert.DoesNotContain("2. ", lista);
        await EscribirAsync(_conductor, "1");
        Assert.Contains("Aceptaste", await EscribirAsync(_conductor, "1"));
        Assert.Equal(new AceptarAsignacionDominio(primera.Id, _conductor.Id), Assert.Single(_prueba.Aceptar.Recibidos));

        await EscribirAsync(_conductor, "1");
        await EscribirAsync(_conductor, "1");
        Assert.Contains("Por qué", await EscribirAsync(_conductor, "2"));
        Assert.Contains("otro conductor", await EscribirAsync(_conductor, "Vehículo en taller"));
        Assert.Equal(new RechazarAsignacionDominio(primera.Id, "Vehículo en taller", _conductor.Id), Assert.Single(_prueba.Rechazar.Recibidos));
    }

    [Fact]
    public async Task Conductor_IniciaFinalizaYReportaNovedades()
    {
        var programado = Viaje(EstadoViaje.Programado);
        var enCurso = Viaje(EstadoViaje.EnCurso, horas: -1);
        _prueba.Lecturas.Viajes.AddRange([programado, enCurso, Viaje(EstadoViaje.Programado, conductorId: Guid.NewGuid())]);

        await EscribirAsync(_conductor, "2");
        Assert.Contains("Viaje iniciado", await EscribirAsync(_conductor, "1"));
        await EscribirAsync(_conductor, "3");
        Assert.Contains("Viaje finalizado", await EscribirAsync(_conductor, "1"));
        var novedades = await EscribirAsync(_conductor, "4");
        Assert.Contains("2. ", novedades);
        Assert.Contains("Falla mecánica", await EscribirAsync(_conductor, "2"));
        Assert.Equal(TextosBot.OpcionInvalida, await EscribirAsync(_conductor, "7"));
        await EscribirAsync(_conductor, "2");
        Assert.Contains("avisamos al operador", await EscribirAsync(_conductor, "Se pinchó una llanta"));

        Assert.Equal(programado.Id, Assert.Single(_prueba.Iniciar.Recibidos).ViajeId);
        Assert.Equal(enCurso.Id, Assert.Single(_prueba.Finalizar.Recibidos).ViajeId);
        Assert.Equal(new ReportarIncidenteDominio(programado.Id, TipoIncidente.FallaMecanica, "Se pinchó una llanta", _conductor.Id),
            Assert.Single(_prueba.Reportar.Recibidos));
    }

    [Fact]
    public async Task Conductor_SinViajes_NoTieneQueElegir()
    {
        Assert.Contains("No tienes asignaciones por responder", await EscribirAsync(_conductor, "1"));
        Assert.Contains("No tienes viajes aceptados", await EscribirAsync(_conductor, "2"));
        Assert.Contains("No tienes viajes en curso.", await EscribirAsync(_conductor, "3"));
        Assert.Contains("ni por iniciar", await EscribirAsync(_conductor, "4"));
    }

    [Fact]
    public async Task MensajeNoTexto_ConversacionVencida_YOpcionDesconocida()
    {
        await EscribirAsync(_pasajero, "1");
        Assert.Equal(TextosBot.SoloTexto, await EscribirAsync(_pasajero, "", MensajeRecibido.TipoNoSoportado));
        Assert.Equal(PasoConversacion.PedirOrigen, _conversacion.Paso);

        var tarde = Ahora + Conversacion.Inactividad + TimeSpan.FromMinutes(1);
        Assert.Contains("¿Qué quieres hacer?", await EscribirAsync(_pasajero, "Hotel Centro", cuando: tarde));
        Assert.Equal(PasoConversacion.Menu, _conversacion.Paso);
        Assert.Contains("¿Qué quieres hacer?", await EscribirAsync(_pasajero, "9", cuando: tarde));

        var panel = new Usuario(Guid.NewGuid(), "Admin", "+57", RolesSistema.AdministradorId, Ahora);
        Assert.Equal(TextosBot.NoRegistrado, await EscribirAsync(panel, "1", cuando: tarde));
    }

    [Fact]
    public async Task UnaAccionQueFalla_PropagaLaReglaDeNegocio()
    {
        _prueba.Lecturas.Viajes.Add(Viaje(EstadoViaje.Programado));
        _prueba.Iniciar.Falla = new FlowRideExcepcion("El viaje está Cancelado.");
        await EscribirAsync(_conductor, "2");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => EscribirAsync(_conductor, "1"));
    }
}
