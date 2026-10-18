using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Priority;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Assignment.Rules.TieBreaking;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.UnitTests.Domain.Assignment.Escenario;

namespace FlowRide.UnitTests.Domain.Assignment;

public class AssignmentEngineTests
{
    [Fact]
    public void ConfiguracionInvalida_NoAsigna_YExplicaElMotivo()
    {
        var configuracion = new AssignmentConfiguration("tenant-a", 2,
            [new RuleSetting(WorkloadBalanceRule.RuleName, true, 1, 1m)]);

        var result = Motor().Decide(Contexto(Solicitud(), configuracion, Candidato(1)));

        Assert.Equal(AssignmentOutcome.InvalidConfiguration, result.Outcome);
        Assert.Null(result.Selected);
        Assert.Contains("eliminatoria", result.Reason);
        Assert.Equal(2, result.DecisionLog.ConfigurationVersion);
        Assert.Single(result.DecisionLog.InitialCandidates);
    }

    [Fact]
    public void SinCandidatos_NoAsigna()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion()));

        Assert.Equal(AssignmentOutcome.NoCandidates, result.Outcome);
        Assert.Contains("No se recibieron candidatos", result.Reason);
    }

    [Fact]
    public void TodosDescartados_NoAsigna_YRegistraCadaMotivo()
    {
        var result = Motor().Decide(Contexto(Solicitud(pasajeros: 6), Configuracion(),
            Candidato(1, capacidad: 4),
            Candidato(2, estadoConductor: EstadoConductor.Inactivo)));

        Assert.Equal(AssignmentOutcome.NoCandidates, result.Outcome);
        Assert.Null(result.Selected);
        Assert.Collection(result.DecisionLog.Discarded,
            d => Assert.StartsWith(ServiceCapabilityRule.RuleName, d.Reason),
            d => Assert.StartsWith(AvailabilityRule.RuleName, d.Reason));
        Assert.Empty(result.DecisionLog.Scores);
    }

    [Fact]
    public void DecisionLog_RegistraElResultadoDeCadaReglaEnOrden()
    {
        var result = Motor().Decide(Contexto(Solicitud(urgente: true), Configuracion(), Candidato(1)));

        var evaluacion = Assert.Single(result.DecisionLog.Evaluations);
        Assert.Equal(
            new[]
            {
                AvailabilityRule.RuleName, ShiftCompatibilityRule.RuleName, RegulatoryRestrictionRule.RuleName,
                ServiceCapabilityRule.RuleName, CriticalServicePriorityRule.RuleName, WorkloadBalanceRule.RuleName
            },
            evaluacion.RuleOutcomes.Select(o => o.RuleName));
        Assert.All(evaluacion.RuleOutcomes.Take(4), o => Assert.True(o.Passed));
        var carga = evaluacion.RuleOutcomes.Last();
        Assert.Equal((100m, 0.5m, 50m), (carga.RawValue!.Value, carga.Weight!.Value, carga.WeightedValue!.Value));
        Assert.Equal(1, evaluacion.Rank);
    }

    [Fact]
    public void EliminatoriaDescartante_SeEvaluanIgualLasDemasEliminatorias()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(),
            Candidato(1, estadoConductor: EstadoConductor.Suspendido, capacidad: 1), Candidato(2)));

        var descartado = result.DecisionLog.Evaluations.Single(e => !e.Eligible);
        Assert.Equal(4, descartado.RuleOutcomes.Count);
        Assert.Equal(new bool?[] { false, true, true, false }, descartado.RuleOutcomes.Select(o => o.Passed));
        Assert.StartsWith(AvailabilityRule.RuleName, descartado.DiscardReason);
    }

    [Fact]
    public void ReglaDeshabilitada_NoSeEjecuta()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(cargaHabilitada: false), Candidato(1)));

        Assert.True(result.IsAssigned);
        Assert.DoesNotContain(result.DecisionLog.Evaluations.Single().RuleOutcomes, o => o.RuleName == WorkloadBalanceRule.RuleName);
    }

    [Fact]
    public void OrdenDeLaConfiguracion_DeterminaElOrdenDeEjecucion()
    {
        var configuracion = new AssignmentConfiguration("tenant-a", 1,
        [
            new RuleSetting(AvailabilityRule.RuleName, true, 20, 1m),
            new RuleSetting(ServiceCapabilityRule.RuleName, true, 10, 1m),
            new RuleSetting(ShiftCompatibilityRule.RuleName, true, 30, 1m),
            new RuleSetting(RegulatoryRestrictionRule.RuleName, true, 40, 1m)
        ]);

        var result = Motor().Decide(Contexto(Solicitud(pasajeros: 9), configuracion,
            Candidato(1, estadoConductor: EstadoConductor.Suspendido)));

        var evaluacion = result.DecisionLog.Evaluations.Single();
        Assert.Equal(new[] { ServiceCapabilityRule.RuleName, AvailabilityRule.RuleName, ShiftCompatibilityRule.RuleName,
            RegulatoryRestrictionRule.RuleName }, evaluacion.RuleOutcomes.Select(o => o.RuleName));
        Assert.StartsWith(ServiceCapabilityRule.RuleName, evaluacion.DiscardReason);
    }

    [Fact]
    public void Empate_SinDesempatadorHabilitado_ElMotorUsaOrdenEstable()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(desempateHabilitado: false),
            Candidato(2, asignacionesRecientes: 1), Candidato(1, asignacionesRecientes: 1)));

        Assert.Equal(Id(1), result.Selected!.Conductor.Id);
        Assert.Equal(nameof(AssignmentEngine), result.DecisionLog.TieBreak!.DecidedBy);
    }

    [Fact]
    public void Empate_ConCargaDeshabilitada_DecideMenosAsignacionesRecientes()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(cargaHabilitada: false),
            Candidato(1, asignacionesRecientes: 4), Candidato(2, asignacionesRecientes: 1)));

        Assert.Equal(Id(2), result.Selected!.Conductor.Id);
        Assert.Equal(StableTieBreaker.ByRecentAssignments, result.DecisionLog.TieBreak!.Criterion);
    }

    [Fact]
    public void Empate_ConTodoIgual_DecideElIdDelConductor()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(), Candidato(7), Candidato(3)));

        Assert.Equal(Id(3), result.Selected!.Conductor.Id);
        Assert.Equal(StableTieBreaker.ByConductorId, result.DecisionLog.TieBreak!.Criterion);
    }

    [Fact]
    public void DecisionLog_ConservaCandidatosIniciales_OrdenadosDeFormaEstable()
    {
        var result = Motor().Decide(Contexto(Solicitud(), Configuracion(), Candidato(3), Candidato(1), Candidato(2)));

        Assert.Equal(new[] { Id(1), Id(2), Id(3) }, result.DecisionLog.InitialCandidates.Select(c => c.ConductorId));
        Assert.Equal(Id(900), result.DecisionLog.SolicitudId);
        Assert.Equal(AssignmentOutcome.Assigned, result.DecisionLog.Outcome);
        Assert.Equal(result.Selected!.Reference, result.DecisionLog.Selected);
    }

    [Fact]
    public void CatalogoConReglasRepetidas_SeRechaza() =>
        Assert.Throws<ArgumentException>(() => new AssignmentEngine([new AvailabilityRule(), new AvailabilityRule()]));

    [Fact]
    public void ReglaPersonalizada_SeOrquestaSinCambiarElMotor()
    {
        IAssignmentRule[] catalogo = [.. DefaultAssignmentRules.Create(), new SoloConductorParRule()];
        var configuracion = new AssignmentConfiguration("tenant-a", 1,
            [.. Configuracion().Rules, new RuleSetting(SoloConductorParRule.RuleName, true, 99, 1m)]);

        var result = new AssignmentEngine(catalogo).Decide(Contexto(Solicitud(), configuracion, Candidato(1), Candidato(2)));

        Assert.Equal(Id(2), result.Selected!.Conductor.Id);
    }

    private sealed class SoloConductorParRule : IFilteringRule
    {
        public const string RuleName = "SoloConductorPar";

        public string Name => RuleName;

        public FilterResult Evaluate(AssignmentContext context, Candidato candidato) =>
            candidato.Conductor.Id.ToString().EndsWith('2') ? FilterResult.Pass("Par") : FilterResult.Fail("Impar");
    }
}
