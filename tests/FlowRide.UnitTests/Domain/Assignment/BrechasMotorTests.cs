using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Priority;
using static FlowRide.UnitTests.Domain.Assignment.Escenario;

namespace FlowRide.UnitTests.Domain.Assignment;

/// <summary>Brechas del motor frente al ADR-0009 que cierra la Fase 5.</summary>
public class BrechasMotorTests
{
    private sealed class ReglaFalsa(string nombre, params string[] dependencias) : IScoringRule
    {
        public string Name => nombre;
        public IReadOnlyCollection<string> Dependencias => dependencias;
        public RuleScore Score(AssignmentContext context, Candidato candidato) => new(RuleScore.Max, "Prueba.");
    }

    private static ValidationResult Validar(IEnumerable<IAssignmentRule> catalogo, params RuleSetting[] extra) =>
        new ConfigurationValidator().Validate(new AssignmentConfiguration("tenant-a", 1, [.. Eliminatorias(), .. extra]), catalogo.ToList());

    [Fact]
    public void ReglaObligatoriaDeshabilitada_SeRechaza()
    {
        var configuracion = new AssignmentConfiguration("tenant-a", 1,
            Eliminatorias().Select(r => r.Name == RegulatoryRestrictionRule.RuleName ? new RuleSetting(r.Name, false, r.Order, r.Weight) : r));

        var resultado = new ConfigurationValidator().Validate(configuracion, DefaultAssignmentRules.Create());

        Assert.Contains($"La regla obligatoria '{RegulatoryRestrictionRule.RuleName}' debe estar habilitada.", resultado.Errors);
    }

    [Fact]
    public void DependenciaDeshabilitada_YOrdenNoPositivo_SeRechazan()
    {
        IAssignmentRule[] catalogo = [.. DefaultAssignmentRules.Create(), new ReglaFalsa("Base"), new ReglaFalsa("Derivada", "Base")];

        var sinBase = Validar(catalogo, new RuleSetting("Derivada", true, 1, 1m), new RuleSetting("Base", false, 2, 1m));
        var conBase = Validar(catalogo, new RuleSetting("Derivada", true, 1, 1m), new RuleSetting("Base", true, 2, 1m));
        var ordenCero = Validar(catalogo, new RuleSetting("Base", true, 0, 1m));

        Assert.Contains("La regla 'Derivada' necesita la regla 'Base' habilitada.", sinBase.Errors);
        Assert.True(conBase.IsValid);
        Assert.Contains("El orden de la regla 'Base' debe ser mayor que cero.", ordenCero.Errors);
    }

    [Fact]
    public void CicloDeDependencias_SeDetecta()
    {
        IAssignmentRule[] catalogo = [.. DefaultAssignmentRules.Create(), new ReglaFalsa("A", "B"), new ReglaFalsa("B", "C"), new ReglaFalsa("C", "A")];

        var resultado = Validar(catalogo, new RuleSetting("A", true, 1, 1m));

        Assert.Contains("Hay un ciclo de dependencias entre reglas: A -> B -> C -> A.", resultado.Errors);
    }

    [Fact]
    public void ElDecisionLog_RegistraLaHoraYLaVersionDeCadaRegla()
    {
        var evaluadaEn = HoraServicio.AddHours(-3);

        var resultado = Motor().Decide(new AssignmentContext(Solicitud(urgente: true), [Candidato(1, aptoCritico: true)], Configuracion(), evaluadaEn));

        Assert.Equal(evaluadaEn, resultado.DecisionLog.EvaluatedAt);
        var resultados = resultado.DecisionLog.Evaluations.Single().RuleOutcomes;
        Assert.Equal(2, resultados.Single(r => r.RuleName == AvailabilityRule.RuleName).RuleVersion);
        Assert.Equal(2, resultados.Single(r => r.RuleName == CriticalServicePriorityRule.RuleName).RuleVersion);
        Assert.Equal(1, resultados.Single(r => r.RuleName == ServiceCapabilityRule.RuleName).RuleVersion);
        Assert.Equal(HoraServicio.AddDays(-1), new AssignmentContext(Solicitud(), [], Configuracion()).EvaluadaEn);
    }

    [Theory]
    [InlineData(true, false, "conductor ya tiene una asignación")]
    [InlineData(false, true, "ya tiene una asignación vigente")]
    public void UnCandidatoOcupado_SeDescartaConSuMotivo(bool conductorOcupado, bool vehiculoOcupado, string motivo)
    {
        var resultado = Motor().Decide(Contexto(Solicitud(), Configuracion(),
            Candidato(1, conductorOcupado: conductorOcupado, vehiculoOcupado: vehiculoOcupado)));

        Assert.Equal(AssignmentOutcome.NoCandidates, resultado.Outcome);
        Assert.Contains(motivo, resultado.DecisionLog.Discarded.Single().Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void LasReglasObligatoriasSonLasCuatroEliminatorias() =>
        Assert.Equal(
            [AvailabilityRule.RuleName, RegulatoryRestrictionRule.RuleName, ServiceCapabilityRule.RuleName, ShiftCompatibilityRule.RuleName],
            DefaultAssignmentRules.Create().Where(r => r.Obligatoria).Select(r => r.Name).Order(StringComparer.Ordinal));
}
