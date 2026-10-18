namespace FlowRide.Domain.Assignment.Rules.Filtering;

/// <summary>La capacidad del vehículo es mayor o igual a la cantidad de pasajeros.</summary>
public sealed class ServiceCapabilityRule : IFilteringRule
{
    public const string RuleName = nameof(ServiceCapabilityRule);

    public string Name => RuleName;

    /// <summary>Obligatoria (ADR-0009): ninguna configuración puede deshabilitarla.</summary>
    public bool Obligatoria => true;

    public FilterResult Evaluate(AssignmentContext context, Candidato candidato)
    {
        var pasajeros = context.Solicitud.CantidadPasajeros;
        var capacidad = candidato.Vehiculo.Capacidad;

        return candidato.Vehiculo.AdmiteCapacidad(pasajeros)
            ? FilterResult.Pass($"Capacidad {capacidad} ≥ {pasajeros} pasajeros.")
            : FilterResult.Fail($"Capacidad {capacidad} < {pasajeros} pasajeros.");
    }
}
