using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.TieBreaking;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.UnitTests.Domain.Assignment.Escenario;

namespace FlowRide.UnitTests.Domain.Assignment;

/// <summary>Escenarios de la PoC del ADR-0009.</summary>
public class AssignmentEnginePocTests
{
    [Fact]
    public void Escenario1_ConductorNoDisponible_QuedaExcluido()
    {
        // El conductor 1 tendría mejor puntaje, pero está suspendido.
        var suspendido = Candidato(1, estadoConductor: EstadoConductor.Suspendido, asignacionesRecientes: 0);
        var disponible = Candidato(2, asignacionesRecientes: 3);

        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(), suspendido, disponible));

        Assert.True(result.IsAssigned);
        Assert.Equal(Id(2), result.Selected!.Conductor.Id);
        var descartado = Assert.Single(result.DecisionLog.Discarded);
        Assert.Equal(suspendido.Reference, descartado.Candidate);
        Assert.StartsWith(AvailabilityRule.RuleName, descartado.Reason);
        Assert.DoesNotContain(result.DecisionLog.Scores, s => s.Candidate == suspendido.Reference);
    }

    [Fact]
    public void Escenario2_BalanceDeCarga_FavoreceAlConductorConMenosCarga()
    {
        var cargado = Candidato(1, asignacionesRecientes: 5);
        var liviano = Candidato(2, asignacionesRecientes: 1);

        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(), cargado, liviano));

        Assert.Equal(Id(2), result.Selected!.Conductor.Id);
        Assert.Null(result.DecisionLog.TieBreak);
        Assert.Collection(result.DecisionLog.Scores,
            primero => Assert.Equal((liviano.Reference, 25m), (primero.Candidate, primero.Score)),       // 100/2 × 0.5
            segundo => Assert.Equal((cargado.Reference, 8.3334m), (segundo.Candidate, segundo.Score)));  // 16.6667 × 0.5, redondeo a 4 decimales
    }

    [Fact]
    public void Escenario3_ServicioCritico_ModificaLaPrioridadSegunConfiguracion()
    {
        var enTurnoActivo = Candidato(1, estadoTurno: EstadoTurno.Activo, asignacionesRecientes: 3);
        var turnoProgramado = Candidato(2, estadoTurno: EstadoTurno.Programado, asignacionesRecientes: 0);
        var configuracion = Configuracion(pesoCritico: 1m, pesoCarga: 0.5m);
        var motor = Motor();

        var normal = motor.Decide(Contexto(Solicitud(urgente: false), configuracion, enTurnoActivo, turnoProgramado));
        var critica = motor.Decide(Contexto(Solicitud(urgente: true), configuracion, enTurnoActivo, turnoProgramado));
        var criticaSinRegla = motor.Decide(Contexto(
            Solicitud(urgente: true), Configuracion(criticoHabilitado: false), enTurnoActivo, turnoProgramado));

        Assert.Equal(Id(2), normal.Selected!.Conductor.Id);
        Assert.Equal(Id(1), critica.Selected!.Conductor.Id);
        Assert.Equal(Id(2), criticaSinRegla.Selected!.Conductor.Id);

        var prioridad = critica.DecisionLog.Scores.Single(s => s.Candidate == enTurnoActivo.Reference);
        Assert.Equal(100m, prioridad.PriorityScore);   // 100 × peso 1
        Assert.Equal(112.5m, prioridad.TotalScore);    // + 25 × 0.5
    }

    [Fact]
    public void Escenario4_Empate_SeAplicaStableTieBreaker_YEsDeterminista()
    {
        var reciente = Candidato(1, asignacionesRecientes: 2, ultimaAsignacion: HoraServicio.AddHours(-1));
        var antiguo = Candidato(2, asignacionesRecientes: 2, ultimaAsignacion: HoraServicio.AddDays(-3));
        var nunca = Candidato(3, asignacionesRecientes: 2, ultimaAsignacion: null);
        var motor = Motor();
        var solicitud = Solicitud();
        var configuracion = Configuracion();

        var resultados = new[]
        {
            motor.Decide(Contexto(solicitud, configuracion, reciente, antiguo, nunca)),
            motor.Decide(Contexto(solicitud, configuracion, nunca, reciente, antiguo)),
            motor.Decide(Contexto(solicitud, configuracion, antiguo, nunca, reciente))
        };

        foreach (var result in resultados)
        {
            Assert.Equal(Id(3), result.Selected!.Conductor.Id);
            var tieBreak = Assert.IsType<TieBreakRecord>(result.DecisionLog.TieBreak);
            Assert.Equal(StableTieBreaker.RuleName, tieBreak.DecidedBy);
            Assert.Equal(StableTieBreaker.ByLongestIdle, tieBreak.Criterion);
            Assert.Equal(new[] { nunca.Reference, antiguo.Reference, reciente.Reference }, tieBreak.TiedCandidates);
            Assert.Equal(
                resultados[0].DecisionLog.Scores.Select(s => (s.Candidate, s.TotalScore, s.Rank)),
                result.DecisionLog.Scores.Select(s => (s.Candidate, s.TotalScore, s.Rank)));
            Assert.Equal(resultados[0].Reason, result.Reason);
        }
    }

    [Theory]
    [InlineData(0.6, 0.4, 1)] // A = 100×0.6 + 20×0.4 = 68   vs  B = 100×0.4 = 40
    [InlineData(0.2, 0.9, 2)] // A = 100×0.2 + 20×0.9 = 38   vs  B = 100×0.9 = 90
    public void Escenario5_ReglasEnConflicto_SeResuelvenPorPesos(double pesoCritico, double pesoCarga, int esperado)
    {
        // Prioridad favorece a A (turno activo); balance de carga favorece a B (sin carga).
        var a = Candidato(1, estadoTurno: EstadoTurno.Activo, asignacionesRecientes: 4);
        var b = Candidato(2, estadoTurno: EstadoTurno.Programado, asignacionesRecientes: 0);
        var configuracion = Configuracion(pesoCritico: (decimal)pesoCritico, pesoCarga: (decimal)pesoCarga);

        var result = Motor().Decide(Contexto(Solicitud(urgente: true), configuracion, a, b));

        Assert.Equal(Id(esperado), result.Selected!.Conductor.Id);
        Assert.Null(result.DecisionLog.TieBreak);
    }

    [Fact]
    public void Escenario6_DosTenantsConConfiguracionesDistintas_DecidenDistinto()
    {
        var enTurnoActivo = Candidato(1, estadoTurno: EstadoTurno.Activo, asignacionesRecientes: 2);
        var descansado = Candidato(2, estadoTurno: EstadoTurno.Programado, asignacionesRecientes: 0);
        var solicitud = Solicitud(urgente: true);
        var tenantA = Configuracion("tenant-a", version: 3, pesoCritico: 0.9m, pesoCarga: 0.3m);
        var tenantB = Configuracion("tenant-b", version: 7, criticoHabilitado: false, pesoCarga: 1m);
        var motor = Motor();

        var resultA = motor.Decide(Contexto(solicitud, tenantA, enTurnoActivo, descansado));
        var resultB = motor.Decide(Contexto(solicitud, tenantB, enTurnoActivo, descansado));

        Assert.Equal(Id(1), resultA.Selected!.Conductor.Id);
        Assert.Equal(Id(2), resultB.Selected!.Conductor.Id);
        Assert.Equal(("tenant-a", 3), (resultA.DecisionLog.TenantId, resultA.DecisionLog.ConfigurationVersion));
        Assert.Equal(("tenant-b", 7), (resultB.DecisionLog.TenantId, resultB.DecisionLog.ConfigurationVersion));
    }
}
