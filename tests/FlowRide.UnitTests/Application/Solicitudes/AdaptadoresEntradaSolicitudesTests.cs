using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Adaptadores;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Solicitudes;

public class AdaptadoresEntradaSolicitudesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
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

    private static readonly SolicitudViaje Solicitud = new(Guid.NewGuid(), Guid.NewGuid(), "Hotel", "Aeropuerto", Ahora.AddDays(1),
        TimeSpan.FromMinutes(45), 2, CanalSolicitud.PanelWeb, Ahora);

    private static SolicitudDetalleLectura Detalle(Guid id) => new(
        new SolicitudLectura(id, Solicitud.PasajeroId, "Ana", null, null, null, null, null, Ahora.AddDays(1), TimeSpan.FromMinutes(44.6),
            2, false, true, CanalSolicitud.PanelWeb, EstadoSolicitud.Pendiente, Ahora, DetalleRestringido: true),
        [new CambioEstadoLectura(null, EstadoSolicitud.Pendiente, Ahora, Actor, null)]);

    private readonly Grabador<RegistrarSolicitudDominio, SolicitudViaje> _registrar = new(_ => Solicitud);
    private readonly Grabador<ModificarSolicitudDominio, SolicitudViaje> _modificar = new(_ => Solicitud);
    private readonly Grabador<CancelarSolicitudDominio, SolicitudViaje> _cancelar = new(_ => Solicitud);
    private readonly Grabador<ConsultarSolicitudDominio, SolicitudDetalleLectura> _consultar = new(d => Detalle(d.SolicitudId));
    private readonly Grabador<BuscarSolicitudesDominio, PaginaResultado<SolicitudLectura>> _buscar =
        new(d => new PaginaResultado<SolicitudLectura>([Detalle(Guid.NewGuid()).Solicitud], 1, d.Paginacion));

    private SolicitudesAdaptadorEntrada Adaptador() => new(_registrar, _modificar, _cancelar, _consultar, _buscar);

    [Fact]
    public async Task Registrar_EsDelPanelWeb_YDevuelveElDetalleComoLoVeElLector()
    {
        var respuesta = await Adaptador().RegistrarAsync(new SolicitudPeticion
        {
            PasajeroId = Solicitud.PasajeroId,
            DireccionOrigen = " Hotel ",
            DireccionDestino = " ",
            FechaHoraServicio = Ahora,
            DuracionMinutos = 45,
            CantidadPasajeros = 2
        }, Actor, RolesPanel.Administrador, default);

        var datos = Assert.Single(_registrar.Recibidos);
        Assert.Equal((CanalSolicitud.PanelWeb, "Hotel", null, Actor), (datos.Canal, datos.DireccionOrigen, datos.DireccionDestino, datos.ActorId));
        Assert.Equal(TimeSpan.FromMinutes(45), datos.DuracionEstimada);
        Assert.Equal(LectorSolicitud.Administrador, Assert.Single(_consultar.Recibidos).Lector);
        Assert.Equal(45, respuesta.Solicitud.DuracionMinutos);
        Assert.True(respuesta.Solicitud.DetalleRestringido);
        Assert.Equal("Pendiente", Assert.Single(respuesta.Historial).EstadoNuevo);
    }

    [Fact]
    public async Task Registrar_YModificar_ExigenSusCampos()
    {
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Adaptador().RegistrarAsync(new SolicitudPeticion { CantidadPasajeros = 1 }, Actor, "", default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Adaptador().RegistrarAsync(new SolicitudPeticion { PasajeroId = Guid.NewGuid(), CantidadPasajeros = 1 }, null, "", default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Adaptador().ModificarAsync(Solicitud.Id, new SolicitudPeticion { DireccionOrigen = "A" }, Actor, "", default));
    }

    [Fact]
    public async Task Modificar_ConvierteLosMinutos()
    {
        await Adaptador().ModificarAsync(Solicitud.Id, new SolicitudPeticion
        {
            DireccionOrigen = "A",
            DireccionDestino = "B",
            FechaHoraServicio = Ahora,
            DuracionMinutos = 30,
            CantidadPasajeros = 1,
            Urgente = true
        }, Actor, RolesPanel.OperadorLogistico, default);

        var datos = Assert.Single(_modificar.Recibidos);
        Assert.Equal((TimeSpan.FromMinutes(30), true), (datos.DuracionEstimada, datos.Urgente));
        Assert.Equal(LectorSolicitud.OperadorLogistico, _consultar.Recibidos[0].Lector);
    }

    [Theory]
    [InlineData(RolesPanel.OperadorLogistico, true)]
    [InlineData(RolesPanel.Administrador, false)]
    public async Task Cancelar_SoloElOperadorLogisticoSaltaLaVentana(string rol, bool salta)
    {
        await Adaptador().CancelarAsync(Solicitud.Id, new CancelarSolicitudPeticion { Motivo = " Contingencia " }, Actor, rol, default);

        var datos = Assert.Single(_cancelar.Recibidos);
        Assert.Equal(("Contingencia", salta), (datos.Motivo, datos.PuedeSaltarVentana));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Adaptador().CancelarAsync(Solicitud.Id, new CancelarSolicitudPeticion(), Actor, rol, default));
    }

    [Fact]
    public async Task Buscar_ValidaElEstadoYPagina()
    {
        var pagina = await Adaptador().BuscarAsync(new BusquedaSolicitudesPeticion { Estado = "pendiente", Pagina = 2, TamanoPagina = 10 },
            RolesPanel.OperadorLogistico, default);

        var datos = Assert.Single(_buscar.Recibidos);
        Assert.Equal((EstadoSolicitud.Pendiente, 2, 10), (datos.Filtro.Estado, datos.Paginacion.Pagina, datos.Paginacion.Tamano));
        Assert.Equal(1, pagina.Total);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Adaptador().BuscarAsync(new BusquedaSolicitudesPeticion { Estado = "Perdida" }, RolesPanel.Administrador, default));
    }

    [Fact]
    public async Task Itinerarios_InterpretanLosDiasEnEspanol()
    {
        var empresaId = Guid.NewGuid();
        var registrar = new Grabador<RegistrarItinerariosDominio, IReadOnlyList<Itinerario>>(d =>
            [new Itinerario(Guid.NewGuid(), d.EmpresaClienteId, d.CodigoServicio, d.FechaDesde, d.Origen, d.Destino, d.HoraSalida, d.HoraLlegada)]);
        var buscar = new Grabador<BuscarItinerariosDominio, PaginaResultado<Itinerario>>(d => new PaginaResultado<Itinerario>([], 0, d.Paginacion));
        var adaptador = new ItinerariosAdaptadorEntrada(registrar, buscar);
        var peticion = new ItinerariosPeticion
        {
            EmpresaClienteId = empresaId,
            CodigoServicio = " S-1 ",
            Origen = "Base",
            Destino = "Mina",
            HoraSalida = new TimeOnly(6, 0),
            HoraLlegada = new TimeOnly(7, 0),
            FechaDesde = new DateOnly(2026, 10, 19),
            Dias = ["Lunes", "miércoles", "SABADO"]
        };

        var creados = await adaptador.RegistrarAsync(peticion, Actor, default);
        await adaptador.BuscarAsync(new BusquedaItinerariosPeticion { EmpresaClienteId = empresaId, CodigoServicio = " " }, default);

        var datos = Assert.Single(registrar.Recibidos);
        Assert.Equal("S-1", datos.CodigoServicio);
        Assert.Equal(datos.FechaDesde, datos.FechaHasta);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Saturday], datos.Dias.Order());
        Assert.Equal("S-1", Assert.Single(creados).CodigoServicio);
        Assert.Null(Assert.Single(buscar.Recibidos).Filtro.CodigoServicio);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(peticion with { Dias = ["feriado"] }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(peticion with { HoraSalida = null }, Actor, default));
    }
}
