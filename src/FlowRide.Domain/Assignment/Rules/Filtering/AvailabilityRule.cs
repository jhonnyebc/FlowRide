namespace FlowRide.Domain.Assignment.Rules.Filtering;

/// <summary>El conductor y el vehículo deben estar activos y libres en la franja de la solicitud.</summary>
public sealed class AvailabilityRule : IFilteringRule
{
    public const string RuleName = nameof(AvailabilityRule);

    public string Name => RuleName;

    /// <summary>Obligatoria (ADR-0009): ninguna configuración puede deshabilitarla.</summary>
    public bool Obligatoria => true;

    /// <summary>v2: también exige que conductor y vehículo estén libres en la franja (Fase 5). v3: descarta al conductor que ya rechazó la solicitud (Fase 6).</summary>
    public int Version => 3;

    public FilterResult Evaluate(AssignmentContext context, Candidato candidato)
    {
        if (!candidato.Conductor.EstaActivo)
            return FilterResult.Fail($"El conductor no está activo (estado {candidato.Conductor.Estado}).");
        if (!candidato.Vehiculo.Activo)
            return FilterResult.Fail($"El vehículo {candidato.Vehiculo.Placa} no está activo.");
        if (candidato.ConductorOcupado)
            return FilterResult.Fail("El conductor ya tiene una asignación vigente en esa franja.");
        if (candidato.VehiculoOcupado)
            return FilterResult.Fail($"El vehículo {candidato.Vehiculo.Placa} ya tiene una asignación vigente en esa franja.");
        if (candidato.RechazoLaSolicitud)
            return FilterResult.Fail("El conductor ya rechazó esta solicitud o la dejó vencer sin responder.");

        return FilterResult.Pass("Conductor y vehículo activos y libres en la franja.");
    }
}
