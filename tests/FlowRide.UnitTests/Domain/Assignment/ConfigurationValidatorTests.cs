using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Scoring;
using static FlowRide.UnitTests.Domain.Assignment.Escenario;

namespace FlowRide.UnitTests.Domain.Assignment;

public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();
    private readonly IReadOnlyList<IAssignmentRule> _catalog = DefaultAssignmentRules.Create();

    private ValidationResult Validar(params RuleSetting[] reglas) =>
        _validator.Validate(new AssignmentConfiguration("tenant-a", 1, reglas), _catalog);

    [Fact]
    public void ConfiguracionCompleta_EsValida()
    {
        var result = _validator.Validate(Configuracion(), _catalog);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReglaDuplicada_SeRechaza()
    {
        var result = Validar(
            new RuleSetting(AvailabilityRule.RuleName, true, 1, 1m),
            new RuleSetting(AvailabilityRule.RuleName, true, 2, 1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicada") && e.Contains(AvailabilityRule.RuleName));
    }

    [Fact]
    public void ReglaInexistente_SeRechaza()
    {
        var result = Validar(
            new RuleSetting(AvailabilityRule.RuleName, true, 1, 1m),
            new RuleSetting("ReglaQueNoExiste", true, 1, 0.5m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("no existe") && e.Contains("ReglaQueNoExiste"));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void PesoFueraDeRango_SeRechaza(double peso)
    {
        var result = Validar([.. Escenario.Eliminatorias(), new RuleSetting(WorkloadBalanceRule.RuleName, true, 1, (decimal)peso)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("fuera de [0, 1]") && e.Contains(WorkloadBalanceRule.RuleName));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PesoEnLosLimites_EsValido(double peso)
    {
        var result = Validar([.. Eliminatorias(), new RuleSetting(WorkloadBalanceRule.RuleName, true, 1, (decimal)peso)]);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SinReglaEliminatoria_SeRechaza()
    {
        var result = Validar(new RuleSetting(WorkloadBalanceRule.RuleName, true, 1, 1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("eliminatoria"));
    }

    [Fact]
    public void EliminatoriasTodasDeshabilitadas_SeRechaza()
    {
        var result = Validar(
            new RuleSetting(AvailabilityRule.RuleName, false, 1, 1m),
            new RuleSetting(WorkloadBalanceRule.RuleName, true, 1, 1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("eliminatoria"));
    }

    [Fact]
    public void DosReglasDelMismoTipoConElMismoOrden_SeRechaza()
    {
        var result = Validar(
            new RuleSetting(AvailabilityRule.RuleName, true, 1, 1m),
            new RuleSetting(ServiceCapabilityRule.RuleName, true, 1, 1m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("comparten el orden 1"));
    }

    [Fact]
    public void AcumulaTodosLosErrores()
    {
        var result = Validar(
            new RuleSetting("Fantasma", true, 1, 2m),
            new RuleSetting("Fantasma", true, 2, 0.5m));

        // Duplicada, inexistente, peso, sin eliminatoria y las cuatro obligatorias sin habilitar.
        Assert.Equal(8, result.Errors.Count);
    }
}
