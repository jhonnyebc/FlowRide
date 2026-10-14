using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Priority;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Assignment.Rules.TieBreaking;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.UnitTests.Domain.Assignment.Escenario;

namespace FlowRide.UnitTests.Domain.Assignment.Rules;

public class CriticalServicePriorityRuleTests
{
    private readonly CriticalServicePriorityRule _rule = new();

    [Theory]
    [InlineData(true, EstadoTurno.Activo, 100)]
    [InlineData(true, EstadoTurno.Programado, 0)]
    [InlineData(false, EstadoTurno.Activo, 0)]
    [InlineData(false, EstadoTurno.Programado, 0)]
    public void SumaPuntosSoloSiLaSolicitudEsCriticaYElConductorEstaEnTurnoActivo(
        bool urgente, EstadoTurno estadoTurno, int esperado)
    {
        var candidato = Candidato(1, estadoTurno: estadoTurno);

        var score = _rule.Evaluate(Contexto(Solicitud(urgente: urgente), Configuracion(), candidato), candidato);

        Assert.Equal(esperado, score.Value);
    }

    [Fact]
    public void EsDeTipoPrioridad() => Assert.Equal(RuleKind.Priority, ((IAssignmentRule)_rule).Kind);
}

public class WorkloadBalanceRuleTests
{
    private readonly WorkloadBalanceRule _rule = new();

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 50)]
    [InlineData(3, 25)]
    [InlineData(5, 16.6667)]
    public void MenosAsignacionesRecientes_MasPuntos(int recientes, double esperado)
    {
        var candidato = Candidato(1, asignacionesRecientes: recientes);

        var score = _rule.Score(Contexto(Solicitud(), Configuracion(), candidato), candidato);

        Assert.Equal((decimal)esperado, score.Value);
    }
}

public class StableTieBreakerTests
{
    private readonly StableTieBreaker _tieBreaker = new();

    private static CandidateEvaluation Evaluacion(int conductor, int recientes, DateTimeOffset? ultima) =>
        new(Candidato(conductor, asignacionesRecientes: recientes, ultimaAsignacion: ultima), true, null, [], 0m, 0m, null);

    [Fact]
    public void Criterio1_MenosAsignacionesRecientesVaPrimero()
    {
        var comparison = _tieBreaker.Compare(Evaluacion(9, 1, HoraServicio), Evaluacion(1, 2, null));

        Assert.True(comparison.Result < 0);
        Assert.Equal(StableTieBreaker.ByRecentAssignments, comparison.Criterion);
    }

    [Fact]
    public void Criterio2_MayorTiempoDesdeLaUltimaAsignacionVaPrimero()
    {
        var comparison = _tieBreaker.Compare(
            Evaluacion(9, 1, HoraServicio.AddDays(-2)), Evaluacion(1, 1, HoraServicio.AddHours(-1)));

        Assert.True(comparison.Result < 0);
        Assert.Equal(StableTieBreaker.ByLongestIdle, comparison.Criterion);
    }

    [Fact]
    public void Criterio2_NuncaAsignadoVaAntesQueCualquierFecha()
    {
        var comparison = _tieBreaker.Compare(Evaluacion(1, 1, HoraServicio.AddYears(-5)), Evaluacion(9, 1, null));

        Assert.True(comparison.Result > 0);
        Assert.Equal(StableTieBreaker.ByLongestIdle, comparison.Criterion);
    }

    [Fact]
    public void Criterio3_IdDelConductor()
    {
        var comparison = _tieBreaker.Compare(Evaluacion(1, 1, null), Evaluacion(2, 1, null));

        Assert.True(comparison.Result < 0);
        Assert.Equal(StableTieBreaker.ByConductorId, comparison.Criterion);
    }

    [Fact]
    public void EsAntisimetrico()
    {
        var a = Evaluacion(1, 1, HoraServicio.AddDays(-1));
        var b = Evaluacion(2, 1, HoraServicio.AddDays(-2));

        Assert.Equal(-Math.Sign(_tieBreaker.Compare(a, b).Result), Math.Sign(_tieBreaker.Compare(b, a).Result));
    }
}
