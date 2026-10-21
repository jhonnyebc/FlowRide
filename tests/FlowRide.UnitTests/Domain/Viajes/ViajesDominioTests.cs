using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes;
using FlowRide.UnitTests.Application.Operacion;
using FlowRide.UnitTests.Domain.Assignment;
using static FlowRide.UnitTests.Domain.Entidades.Datos;

namespace FlowRide.UnitTests.Domain.Viajes;

public class RechazosEnElMotorTests
{
    private static readonly DateTimeOffset Servicio = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Vigente = new(2027, 12, 31);

    private static readonly SolicitudViaje SolicitudDePrueba = new(Guid.NewGuid(), Guid.NewGuid(), "Hotel", "Aeropuerto", Servicio.Local(),
        TimeSpan.FromMinutes(45), 2, CanalSolicitud.PanelWeb, Servicio.AddDays(-1));

    [Fact]
    public void ReglaDeDisponibilidad_DescartaAlConductorQueRechazo()
    {
        var regla = new AvailabilityRule();
        var contexto = new AssignmentContext(Escenario.Solicitud(), [], Escenario.Configuracion());
        var candidato = new Candidato(Escenario.Candidato(1).Conductor, Escenario.Candidato(1).Vehiculo, Escenario.Candidato(1).Turno,
            rechazoLaSolicitud: true);

        var resultado = regla.Evaluate(contexto, candidato);

        Assert.False(resultado.Passed);
        Assert.Contains("rechazó", resultado.Reason, StringComparison.Ordinal);
        Assert.Equal(3, regla.Version);
    }

    [Fact]
    public void Candidatos_MarcanElRechazo_PeroElCandidatoManualNo()
    {
        var conductor = new Conductor(Guid.NewGuid(), "Luis", "+57300", RolesSistema.ConductorId, Servicio, "LIC-1", "C2", Vigente);
        conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Servicio.AddHours(-2), Servicio.AddHours(6)));
        var vehiculo = new Vehiculo(Guid.NewGuid(), "ABC123", "Toyota", "Hiace", 10, Vigente, Vigente, conductorHabitualId: conductor.Id);
        var agenda = new AgendaOperacion(new HashSet<Guid>(), new HashSet<Guid>(), new Dictionary<Guid, int>(), new HashSet<Guid> { conductor.Id });

        var automatico = Assert.Single(AsignacionDominio.Candidatos(SolicitudDePrueba, [conductor], [vehiculo], agenda));
        var manual = AsignacionDominio.CandidatoManual(SolicitudDePrueba, conductor, vehiculo, agenda);

        Assert.True(automatico.RechazoLaSolicitud);
        Assert.False(manual.RechazoLaSolicitud);
    }
}

public class CicloViajeTests
{
    [Fact]
    public void IniciarConElViajeDeOtraAsignacion_Falla()
    {
        var asignacion = Asignacion();
        asignacion.Aceptar(Id(70));
        var ajeno = new Viaje(Id(71), Id(99));

        Assert.Throws<InvalidOperationException>(() => asignacion.Iniciar(ajeno, Ahora));
        Assert.Throws<ArgumentNullException>(() => asignacion.Iniciar(null!, Ahora));
    }

    [Fact]
    public void RegistrarLlegada_SoloEnCurso_YDeUnaParadaDelViaje()
    {
        var asignacion = Asignacion();
        var viaje = asignacion.Aceptar(Id(70));
        var parada = viaje.RegistrarParada(Id(71), "Terminal 2", 1, Ahora.AddHours(4));

        Assert.Throws<InvalidOperationException>(() => viaje.RegistrarLlegada(parada.Id, Ahora));

        asignacion.Iniciar(viaje, Ahora.AddHours(4));
        Assert.Throws<InvalidOperationException>(() => viaje.RegistrarLlegada(Id(72), Ahora));
        Assert.Equal(Ahora.AddHours(4.5), viaje.RegistrarLlegada(parada.Id, Ahora.AddHours(4.5)).HoraReal);
    }

    [Fact]
    public void RespuestaVencida_SoloParaLasAsignadasDespuesDelPlazo()
    {
        var asignacion = Asignacion();
        var plazo = TimeSpan.FromMinutes(15);

        Assert.False(ReglasViaje.RespuestaVencida(asignacion, plazo, Ahora.AddMinutes(14)));
        Assert.True(ReglasViaje.RespuestaVencida(asignacion, plazo, Ahora.AddMinutes(15)));
        asignacion.Aceptar(Id(70));
        Assert.False(ReglasViaje.RespuestaVencida(asignacion, plazo, Ahora.AddDays(1)));
        Assert.Equal("El conductor no respondió en 15 minutos.", ReglasViaje.MotivoVencimiento(plazo));
    }

    [Fact]
    public void Aplicar_ConvierteLasTransicionesInvalidasEnReglasDeNegocio()
    {
        var viaje = ViajeEnCurso();

        var transicion = Assert.Throws<FlowRideExcepcion>(() => ReglasViaje.Aplicar(() => viaje.Iniciar(Ahora)));
        var datos = Assert.Throws<FlowRideExcepcion>(() => ReglasViaje.Aplicar(() => viaje.Finalizar(Ahora)));

        Assert.Contains("EnCurso", transicion.Message, StringComparison.Ordinal);
        Assert.Equal("El fin del viaje no puede ser anterior a su inicio.", datos.Message);
        Assert.Equal(5, ReglasViaje.Aplicar(() => 5));
    }

    [Fact]
    public async Task CancelarProgramado_SoloCancelaViajesQueNoHanEmpezado()
    {
        var viajes = new ViajesEnMemoria();
        var programado = Asignacion().Aceptar(Id(70));
        await viajes.CrearAsync(programado, CancellationToken.None);
        var conViajeEnCurso = new ViajesEnMemoria();
        var enCurso = ViajeEnCurso();
        await conViajeEnCurso.CrearAsync(enCurso, CancellationToken.None);

        Assert.Same(programado, await viajes.CancelarProgramadoDeAsync(programado.AsignacionId, CancellationToken.None));
        Assert.Equal(EstadoViaje.Cancelado, programado.Estado);
        Assert.Null(await viajes.CancelarProgramadoDeAsync(Id(98), CancellationToken.None));
        Assert.Null(await conViajeEnCurso.CancelarProgramadoDeAsync(enCurso.AsignacionId, CancellationToken.None));
        Assert.Equal(EstadoViaje.EnCurso, enCurso.Estado);
    }
}
