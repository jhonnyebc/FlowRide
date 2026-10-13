namespace FlowRide.Domain.Assignment.Rules.Filtering;

/// <summary>El conductor y el vehículo deben estar activos.</summary>
public sealed class AvailabilityRule : IFilteringRule
{
    public const string RuleName = nameof(AvailabilityRule);

    public string Name => RuleName;

    public FilterResult Evaluate(AssignmentContext context, Candidato candidato)
    {
        if (!candidato.Conductor.EstaActivo)
            return FilterResult.Fail($"El conductor no está activo (estado {candidato.Conductor.Estado}).");
        if (!candidato.Vehiculo.Activo)
            return FilterResult.Fail($"El vehículo {candidato.Vehiculo.Placa} no está activo.");

        return FilterResult.Pass("Conductor y vehículo activos.");
    }
}
