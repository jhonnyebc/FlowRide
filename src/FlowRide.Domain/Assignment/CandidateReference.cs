namespace FlowRide.Domain.Assignment;

/// <summary>Identidad de un candidato (conductor + vehículo + turno) para el decision log.</summary>
public sealed record CandidateReference(Guid ConductorId, Guid VehiculoId, Guid TurnoId)
    : IComparable<CandidateReference>
{
    public int CompareTo(CandidateReference? other)
    {
        if (other is null) return 1;

        var byConductor = ConductorId.CompareTo(other.ConductorId);
        if (byConductor != 0) return byConductor;

        var byVehiculo = VehiculoId.CompareTo(other.VehiculoId);
        return byVehiculo != 0 ? byVehiculo : TurnoId.CompareTo(other.TurnoId);
    }
}
