using FlowRide.Domain.Comun;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Domain.Solicitudes;

public class SolicitudModificableTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Operador = Guid.NewGuid();

    private static SolicitudViaje Solicitud(Guid? registradaPor = null) => new(Guid.NewGuid(), Guid.NewGuid(), "Hotel", "Aeropuerto",
        Ahora.AddDays(1).Local(), TimeSpan.FromMinutes(45), 2, CanalSolicitud.PanelWeb, Ahora, registradaPorId: registradaPor);

    [Fact]
    public void ElHistorialInicial_RegistraQuienLaRegistro()
    {
        var solicitud = Solicitud(Operador);
        var propia = Solicitud();

        Assert.Equal(Operador, Assert.Single(solicitud.Historial).ActorId);
        Assert.Equal(propia.PasajeroId, Assert.Single(propia.Historial).ActorId);
        Assert.True(solicitud.EsModificable);
        Assert.True(solicitud.EstaActiva);
    }

    [Fact]
    public void Modificar_PendienteCambiaLosDatosSinCambiarElEstado()
    {
        var solicitud = Solicitud();

        solicitud.Modificar("Oficina", "Planta", Ahora.AddDays(2).Local(), TimeSpan.FromHours(1), 3, true, Operador, Ahora);

        Assert.Equal(("Oficina", "Planta", 3, true), (solicitud.DireccionOrigen, solicitud.DireccionDestino, solicitud.CantidadPasajeros, solicitud.Urgente));
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Single(solicitud.Historial);
    }

    [Fact]
    public void Modificar_SinCandidatosVuelveALaCola()
    {
        var solicitud = Solicitud();
        solicitud.MarcarSinCandidatos("No hay conductores", Ahora);

        solicitud.Modificar("Oficina", "Planta", Ahora.AddDays(2).Local(), TimeSpan.FromHours(1), 1, false, Operador, Ahora);

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal("Solicitud modificada.", solicitud.Historial[^1].Motivo);
    }

    [Fact]
    public void Modificar_AsignadaOValoresInvalidos_Falla()
    {
        var solicitud = Solicitud();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solicitud.Modificar("A", "B", Ahora.Local(), TimeSpan.Zero, 1, false, Operador, Ahora));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            solicitud.Modificar("A", "B", Ahora.Local(), TimeSpan.FromMinutes(1), 0, false, Operador, Ahora));

        var asignacion = new Asignacion(Guid.NewGuid(), solicitud.Id, Guid.NewGuid(), Guid.NewGuid(), solicitud.Franja(),
            OrigenAsignacion.Automatica, Ahora, decisionId: Guid.NewGuid());
        solicitud.MarcarAsignada(asignacion, null, Ahora);

        Assert.False(solicitud.EsModificable);
        Assert.Throws<InvalidOperationException>(() =>
            solicitud.Modificar("A", "B", Ahora.Local(), TimeSpan.FromMinutes(10), 1, false, Operador, Ahora));
        Assert.Throws<FlowRideExcepcion>(() => SolicitudDominio.ValidarModificable(solicitud));
    }
}

public class SolicitudDominioReglasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-1, 45, 2)]
    [InlineData(60, 0, 2)]
    [InlineData(60, 1500, 2)]
    [InlineData(60, 45, 0)]
    public void DatosMinimos_InvalidosSeRechazan(int minutosHastaServicio, int duracionMinutos, int pasajeros) =>
        Assert.Throws<FlowRideExcepcion>(() => SolicitudDominio.ValidarDatosMinimos(
            Ahora.AddMinutes(minutosHastaServicio), TimeSpan.FromMinutes(duracionMinutos), pasajeros, Ahora));

    [Fact]
    public void DatosMinimos_ValidosSeAceptan() =>
        SolicitudDominio.ValidarDatosMinimos(Ahora.AddMinutes(1), TimeSpan.FromMinutes(45), 1, Ahora);

    [Fact]
    public void PuedeSolicitar_SoloPasajeroYEmpresaActivos()
    {
        var empresa = new EmpresaCliente(Guid.NewGuid(), "Aerolínea", "900", "a@a.co");
        var pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", empresa.Id);

        SolicitudDominio.ValidarPuedeSolicitar(pasajero, empresa);
        empresa.Desactivar();
        Assert.Throws<FlowRideExcepcion>(() => SolicitudDominio.ValidarPuedeSolicitar(pasajero, empresa));
        empresa.Activar();
        pasajero.Desactivar();
        Assert.Throws<FlowRideExcepcion>(() => SolicitudDominio.ValidarPuedeSolicitar(pasajero, empresa));
    }

    [Theory]
    [InlineData(false, LectorSolicitud.Administrador, CatalogoParametros.VisiblesSoloParaPasajero, true)]
    [InlineData(true, LectorSolicitud.Pasajero, CatalogoParametros.VisiblesSoloParaPasajero, true)]
    [InlineData(true, LectorSolicitud.OperadorLogistico, CatalogoParametros.VisiblesParaPasajeroYOperadorLogistico, true)]
    [InlineData(true, LectorSolicitud.OperadorLogistico, CatalogoParametros.VisiblesSoloParaPasajero, false)]
    [InlineData(true, LectorSolicitud.Administrador, CatalogoParametros.VisiblesParaPasajeroYOperadorLogistico, false)]
    public void Visibilidad_DeLasSolicitudesPersonales(bool personal, LectorSolicitud lector, string visibilidad, bool puede) =>
        Assert.Equal(puede, VisibilidadSolicitudes.PuedeVerDetalle(personal, lector, visibilidad));
}

public class ItinerarioDominioTests
{
    [Fact]
    public void Fechas_FiltraPorDiasDeLaSemana()
    {
        // 2026-10-19 es lunes.
        var fechas = ItinerarioDominio.Fechas(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25),
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]);

        Assert.Equal([new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 23)], fechas);
        Assert.Equal(7, ItinerarioDominio.Fechas(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25), []).Count);
        Assert.Single(ItinerarioDominio.Fechas(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 19), null));
    }

    [Fact]
    public void Fechas_RangosInvalidos_SeRechazan()
    {
        Assert.Throws<FlowRideExcepcion>(() => ItinerarioDominio.Fechas(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 19), null));
        Assert.Throws<FlowRideExcepcion>(() => ItinerarioDominio.Fechas(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2), null));
        Assert.Throws<FlowRideExcepcion>(() =>
            ItinerarioDominio.Fechas(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20), [DayOfWeek.Sunday]));
    }

    [Fact]
    public void Horario_YEmpresa()
    {
        var empresaId = Guid.NewGuid();
        var itinerario = new Itinerario(Guid.NewGuid(), empresaId, "S-1", new DateOnly(2026, 10, 19), "A", "B", new TimeOnly(6, 0), new TimeOnly(7, 0), HorasDePrueba.Zona);

        ItinerarioDominio.ValidarHorario(new TimeOnly(6, 0), new TimeOnly(7, 0));
        Assert.Throws<FlowRideExcepcion>(() => ItinerarioDominio.ValidarHorario(new TimeOnly(7, 0), new TimeOnly(7, 0)));
        ItinerarioDominio.ValidarDeLaEmpresa(itinerario, empresaId);
        Assert.Throws<FlowRideExcepcion>(() => ItinerarioDominio.ValidarDeLaEmpresa(itinerario, Guid.NewGuid()));
    }

    [Fact]
    public void ZonaHoraria_ConvierteLaHoraLocalDelOperador()
    {
        var zona = new ZonaHorariaOperador(TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"));

        var instante = zona.Instante(new DateOnly(2026, 10, 19), new TimeOnly(6, 30));

        Assert.Equal(new DateTimeOffset(2026, 10, 19, 11, 30, 0, TimeSpan.Zero), instante.ToUniversalTime());
        Assert.Equal(TimeSpan.FromHours(-5), zona.Local(instante).Offset);
    }
}
