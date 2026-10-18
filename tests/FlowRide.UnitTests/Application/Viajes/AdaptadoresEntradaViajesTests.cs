using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Adaptadores;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.UnitTests.Application.Viajes;

public class AdaptadoresEntradaViajesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class Grabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder) : ICasoUsoConRetorno<TEntrada, TSalida>
    {
        public List<TEntrada> Recibidos { get; } = [];

        public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
        {
            Recibidos.Add(datos);
            return Task.FromResult(responder(datos));
        }
    }

    private static Asignacion Asignacion() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new FranjaHoraria(Ahora, Ahora.AddHours(1)), OrigenAsignacion.Manual, Ahora);

    private static readonly Viaje ViajeProgramado = Asignacion().Aceptar(Guid.NewGuid());

    private readonly Grabador<AceptarAsignacionDominio, Viaje> _aceptar = new(_ => ViajeProgramado);
    private readonly Grabador<RechazarAsignacionDominio, Asignacion> _rechazar = new(_ => Asignacion());
    private readonly Grabador<ConsultarSinRespuestaDominio, IReadOnlyList<Guid>> _sinRespuesta = new(_ => [Guid.Empty]);
    private readonly Grabador<VencerAsignacionDominio, bool> _vencer = new(_ => true);
    private readonly Grabador<IniciarViajeDominio, Viaje> _iniciar = new(_ => ViajeProgramado);
    private readonly Grabador<FinalizarViajeDominio, Viaje> _finalizar = new(_ => ViajeProgramado);
    private readonly Grabador<RegistrarParadaDominio, Parada> _parada = new(d => new Parada(Guid.NewGuid(), d.ViajeId, d.Orden, d.Direccion, d.HoraEstimada));
    private readonly Grabador<RegistrarLlegadaDominio, Parada> _llegada = new(d => new Parada(d.ParadaId, d.ViajeId, 1, "Terminal", Ahora));
    private readonly Grabador<ReportarIncidenteDominio, Incidente> _incidente =
        new(d => new Incidente(Guid.NewGuid(), d.ViajeId, d.Tipo, d.Descripcion, Ahora, d.ActorId));
    private readonly Grabador<CalificarViajeDominio, Calificacion> _calificar =
        new(d => new Calificacion(Guid.NewGuid(), d.ViajeId, Guid.NewGuid(), d.Puntaje, d.Comentario, Ahora));
    private readonly Grabador<BuscarViajesDominio, PaginaResultado<ViajeLectura>> _buscar = new(d => new PaginaResultado<ViajeLectura>([], 0, d.Paginacion));
    private readonly Grabador<ConsultarViajeDominio, ViajeDetalleLectura> _consultar = new(_ => null!);
    private readonly Grabador<MonitorearViajesDominio, IReadOnlyList<MonitoreoViajeLectura>> _monitorear = new(_ => []);
    private readonly Grabador<TrazabilidadSolicitudDominio, TrazabilidadLectura> _trazabilidad = new(d => new TrazabilidadLectura(d.SolicitudId,
        EstadoSolicitud.Pendiente, []));

    private ViajesAdaptadorEntrada Adaptador() => new(_aceptar, _rechazar, _sinRespuesta, _vencer, _iniciar, _finalizar, _parada, _llegada,
        _incidente, _calificar, _buscar, _consultar, _monitorear, _trazabilidad);

    [Fact]
    public async Task AccionesDelConductor_ExigenActorYMotivo()
    {
        var adaptador = Adaptador();
        var asignacionId = Guid.NewGuid();

        var viaje = await adaptador.AceptarAsignacionAsync(asignacionId, Actor, default);
        await adaptador.RechazarAsignacionAsync(asignacionId, new MotivoPeticion { Motivo = " En taller " }, Actor, default);

        Assert.Equal((ViajeProgramado.Id, "Programado"), (viaje.Id, viaje.Estado));
        Assert.Equal(new AceptarAsignacionDominio(asignacionId, Actor), Assert.Single(_aceptar.Recibidos));
        Assert.Equal("En taller", Assert.Single(_rechazar.Recibidos).Motivo);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.AceptarAsignacionAsync(asignacionId, null, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RechazarAsignacionAsync(asignacionId, new MotivoPeticion(), Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RechazarAsignacionAsync(asignacionId, null!, Actor, default));
    }

    [Fact]
    public async Task Worker_ConsultaYVenceSinActor()
    {
        var adaptador = Adaptador();

        Assert.Equal([Guid.Empty], await adaptador.SinRespuestaAsync(7, default));
        Assert.True(await adaptador.VencerAsignacionAsync(Guid.Empty, default));
        Assert.Equal(7, Assert.Single(_sinRespuesta.Recibidos).Maximo);
    }

    [Fact]
    public async Task IniciarYFinalizar_PasanElActor()
    {
        var adaptador = Adaptador();
        var viajeId = Guid.NewGuid();

        await adaptador.IniciarAsync(viajeId, Actor, default);
        await adaptador.FinalizarAsync(viajeId, Actor, default);

        Assert.Equal(new IniciarViajeDominio(viajeId, Actor), Assert.Single(_iniciar.Recibidos));
        Assert.Equal(new FinalizarViajeDominio(viajeId, Actor), Assert.Single(_finalizar.Recibidos));
    }

    [Fact]
    public async Task Parada_ValidaLosCampos()
    {
        var adaptador = Adaptador();
        var viajeId = Guid.NewGuid();

        var parada = await adaptador.RegistrarParadaAsync(viajeId,
            new ParadaPeticion { Direccion = " Terminal 2 ", Orden = 1, HoraEstimada = Ahora }, Actor, default);
        var llegada = await adaptador.RegistrarLlegadaAsync(viajeId, parada.Id, Actor, default);

        Assert.Equal(("Terminal 2", 1, viajeId), (parada.Direccion, parada.Orden, parada.ViajeId));
        Assert.Equal(parada.Id, llegada.Id);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarParadaAsync(viajeId, new ParadaPeticion { Orden = 1, HoraEstimada = Ahora },
            Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarParadaAsync(viajeId,
            new ParadaPeticion { Direccion = "X", Orden = 0, HoraEstimada = Ahora }, Actor, default));
    }

    [Theory]
    [InlineData("retraso", null, false)]
    [InlineData("FallaMecanica", "Llanta pinchada", true)]
    [InlineData("Robo", "x", false)]
    [InlineData(null, "x", false)]
    public async Task Incidente_ExigeTipoValidoYDescripcion(string? tipo, string? descripcion, bool valido)
    {
        var adaptador = Adaptador();
        var peticion = new IncidentePeticion { Tipo = tipo, Descripcion = descripcion };

        if (!valido)
        {
            await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ReportarIncidenteAsync(Guid.NewGuid(), peticion, Actor, default));
            return;
        }

        var incidente = await adaptador.ReportarIncidenteAsync(Guid.NewGuid(), peticion, Actor, default);
        Assert.Equal(("FallaMecanica", "Llanta pinchada", Actor), (incidente.Tipo, incidente.Descripcion, incidente.ReportadoPorId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Calificacion_FueraDeRango_Falla(int? puntaje) =>
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Adaptador().CalificarAsync(Guid.NewGuid(), new CalificacionPeticion { Puntaje = puntaje }, Actor, default));

    [Fact]
    public async Task Calificacion_Valida_SeRegistra()
    {
        var calificacion = await Adaptador().CalificarAsync(Guid.NewGuid(), new CalificacionPeticion { Puntaje = 4, Comentario = "Bien" }, Actor, default);

        Assert.Equal((4, "Bien"), (calificacion.Puntaje, calificacion.Comentario));
    }

    [Fact]
    public async Task Consultas_TraducenFiltrosYRol()
    {
        var adaptador = Adaptador();

        await adaptador.BuscarAsync(new BusquedaViajesPeticion { Estado = "encurso", Pagina = 2, TamanoPagina = 10 }, RolesPanel.OperadorLogistico, default);
        await adaptador.ConsultarAsync(Guid.NewGuid(), RolesPanel.Administrador, default);
        await adaptador.MonitorearAsync(RolesPanel.OperadorLogistico, default);
        var solicitudId = Guid.NewGuid();
        Assert.Equal(solicitudId, (await adaptador.TrazabilidadAsync(solicitudId, RolesPanel.Administrador, default)).SolicitudId);

        var busqueda = Assert.Single(_buscar.Recibidos);
        Assert.Equal((EstadoViaje.EnCurso, 2, 10, LectorSolicitud.OperadorLogistico),
            (busqueda.Filtro.Estado, busqueda.Paginacion.Pagina, busqueda.Paginacion.Tamano, busqueda.Lector));
        Assert.Equal(LectorSolicitud.Administrador, Assert.Single(_consultar.Recibidos).Lector);
        Assert.Equal(LectorSolicitud.OperadorLogistico, Assert.Single(_monitorear.Recibidos).Lector);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            adaptador.BuscarAsync(new BusquedaViajesPeticion { Estado = "Volando" }, RolesPanel.Administrador, default));
    }
}
