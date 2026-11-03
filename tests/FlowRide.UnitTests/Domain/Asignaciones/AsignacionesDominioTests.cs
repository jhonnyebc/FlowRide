using System.Text.Json;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.UnitTests.Domain.Assignment;

namespace FlowRide.UnitTests.Domain.Asignaciones;

public class ConfiguracionAsignacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    private static ConfiguracionAsignacion Inicial(int version = 1) =>
        new(Guid.NewGuid(), version, ConfiguracionInicial.Reglas, DefaultAssignmentRules.Create(), null, Ahora);

    [Fact]
    public void NaceEnBorradorConLosTiposDelCatalogo_YSeActivaYArchiva()
    {
        var configuracion = Inicial();

        Assert.Equal(EstadoConfiguracion.Borrador, configuracion.Estado);
        Assert.Equal(RuleKind.Scoring, configuracion.Reglas.Single(r => r.CodigoRegla == WorkloadBalanceRule.RuleName).Tipo);
        configuracion.Activar(Ahora);
        Assert.Equal((EstadoConfiguracion.Activa, Ahora), (configuracion.Estado, configuracion.ActivadaEn));
        Assert.Throws<InvalidOperationException>(() => configuracion.Activar(Ahora));
        configuracion.Archivar();
        Assert.Equal(EstadoConfiguracion.Archivada, configuracion.Estado);
        Assert.Throws<InvalidOperationException>(() => configuracion.Archivar());
    }

    [Fact]
    public void SeConvierteEnLaConfiguracionDelMotor_YExponeSusParametros()
    {
        var configuracion = Inicial(3);

        var delMotor = configuracion.ComoConfiguracionDelMotor("tenant-a");

        Assert.Equal(("tenant-a", 3, 7), (delMotor.TenantId, delMotor.Version, delMotor.Rules.Count));
        Assert.Equal(7, configuracion.ParametroEntero(WorkloadBalanceRule.RuleName, ConfiguracionInicial.ParametroVentanaDias));
        Assert.Null(configuracion.ParametroEntero(WorkloadBalanceRule.RuleName, "otro"));
        Assert.Null(configuracion.ParametroEntero("NoExiste", "x"));
        Assert.Empty(configuracion.Reglas.Single(r => r.CodigoRegla == "AvailabilityRule").LeerParametros());
    }
}

public class AsignacionDominioTests
{
    private static readonly DateTimeOffset Servicio = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Vigente = new(2030, 1, 1);

    private static readonly SolicitudViaje Solicitud = new(Guid.NewGuid(), Guid.NewGuid(), "A", "B", Servicio.Local(), TimeSpan.FromMinutes(45), 2,
        CanalSolicitud.PanelWeb, Servicio.AddDays(-1));

    private static Conductor Conductor(bool conTurno = true)
    {
        var conductor = new Conductor(Guid.NewGuid(), "Luis", "+57300", RolesSistema.ConductorId, Servicio, $"{Guid.NewGuid():N}"[..8], "C2", Vigente);
        if (conTurno) conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Servicio.AddHours(-2), Servicio.AddHours(6)));
        return conductor;
    }

    private static Vehiculo Vehiculo(Guid? habitual = null, bool activo = true) =>
        new(Guid.NewGuid(), $"V{Guid.NewGuid():N}"[..6], "Toyota", "Hiace", 10, Vigente, Vigente, activo, habitual);

    [Fact]
    public void Candidatos_HabitualOLibres_YSinTurnoNoEntra()
    {
        var conHabitual = Conductor();
        var sinHabitual = Conductor();
        var sinTurno = Conductor(conTurno: false);
        var habitual = Vehiculo(conHabitual.Id);
        var libre1 = Vehiculo();
        var libre2 = Vehiculo();
        var inactivo = Vehiculo(activo: false);
        var agenda = new AgendaOperacion(new HashSet<Guid> { sinHabitual.Id }, new HashSet<Guid> { libre2.Id },
            new Dictionary<Guid, int> { [conHabitual.Id] = 3 });

        var candidatos = AsignacionDominio.Candidatos(Solicitud, [conHabitual, sinHabitual, sinTurno], [habitual, libre1, libre2, inactivo], agenda);

        var delHabitual = Assert.Single(candidatos, c => c.Conductor.Id == conHabitual.Id);
        Assert.Equal((habitual.Id, 3), (delHabitual.Vehiculo.Id, delHabitual.AsignacionesRecientes));
        var delOtro = candidatos.Where(c => c.Conductor.Id == sinHabitual.Id).ToList();
        Assert.Equal(2, delOtro.Count);
        Assert.All(delOtro, c => Assert.True(c.ConductorOcupado));
        Assert.True(delOtro.Single(c => c.Vehiculo.Id == libre2.Id).VehiculoOcupado);
        Assert.DoesNotContain(candidatos, c => c.Conductor.Id == sinTurno.Id || c.Vehiculo.Id == inactivo.Id);
    }

    [Fact]
    public void CandidatoManual_ExigeTurno_YSeValidaConElMotor()
    {
        var agenda = new AgendaOperacion(new HashSet<Guid>(), new HashSet<Guid>(), new Dictionary<Guid, int>());
        var candidato = AsignacionDominio.CandidatoManual(Solicitud, Conductor(), Vehiculo(), agenda);

        Assert.Throws<FlowRideExcepcion>(() => AsignacionDominio.CandidatoManual(Solicitud, Conductor(conTurno: false), Vehiculo(), agenda));

        var motor = new AssignmentEngine(DefaultAssignmentRules.Create());
        var bueno = motor.Decide(new AssignmentContext(Solicitud, [candidato], Escenario.Configuracion()));
        AsignacionDominio.ValidarCandidatoManual(bueno);

        var pequeno = new Vehiculo(Guid.NewGuid(), "PEQ001", "Kia", "Picanto", 1, Vigente, Vigente);
        var malo = motor.Decide(new AssignmentContext(Solicitud, [AsignacionDominio.CandidatoManual(Solicitud, Conductor(), pequeno, agenda)],
            Escenario.Configuracion()));
        var error = Assert.Throws<FlowRideExcepcion>(() => AsignacionDominio.ValidarCandidatoManual(malo));
        Assert.Contains("Capacidad 1 < 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LaDecisionPersistente_ConservaEntradaEvaluacionesYResultados()
    {
        var motor = new AssignmentEngine(DefaultAssignmentRules.Create());
        var contexto = new AssignmentContext(Escenario.Solicitud(), [Escenario.Candidato(1), Escenario.Candidato(2, asignacionesRecientes: 1)],
            Escenario.Configuracion(), Servicio.AddHours(-1));
        var resultado = motor.Decide(contexto);

        var decision = DecisionAsignacion.Desde(Guid.NewGuid(), Guid.NewGuid(), OrigenAsignacion.Automatica, contexto, resultado.DecisionLog);

        Assert.Equal((AssignmentOutcome.Assigned, Servicio.AddHours(-1), 1), (decision.Resultado, decision.EvaluadaEn, decision.VersionConfiguracion));
        Assert.Equal(resultado.Selected!.Conductor.Id, decision.ConductorSeleccionadoId);
        Assert.Equal(2, decision.Evaluaciones.Count);
        Assert.Equal(1, decision.Evaluaciones.Single(e => e.ConductorId == decision.ConductorSeleccionadoId).PosicionRanking);
        // Cuatro eliminatorias, prioridad y puntuación; el desempate se registra aparte.
        Assert.All(decision.Evaluaciones, e => Assert.Equal(6, e.Resultados.Count));
        var snapshot = JsonDocument.Parse(decision.EntradaSnapshot).RootElement;
        Assert.Equal(2, snapshot.GetProperty("candidatos").GetArrayLength());
        Assert.Equal(7, snapshot.GetProperty("configuracion").GetProperty("reglas").GetArrayLength());
        Assert.Null(decision.Desempate);
    }

    [Fact]
    public void ConUnEmpate_ElDesempateQuedaGuardado()
    {
        var motor = new AssignmentEngine(DefaultAssignmentRules.Create());
        var contexto = new AssignmentContext(Escenario.Solicitud(), [Escenario.Candidato(1), Escenario.Candidato(2)], Escenario.Configuracion());

        var decision = DecisionAsignacion.Desde(Guid.NewGuid(), Guid.NewGuid(), OrigenAsignacion.Automatica, contexto, motor.Decide(contexto).DecisionLog);

        Assert.Contains("StableTieBreaker", decision.Desempate, StringComparison.Ordinal);
    }
}
