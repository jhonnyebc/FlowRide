namespace FlowRide.Domain.Assignment.Rules.Filtering;

/// <summary>El conductor está habilitado (licencia vigente) y el vehículo tiene RTM y SOAT vigentes en la fecha del servicio.</summary>
public sealed class RegulatoryRestrictionRule : IFilteringRule
{
    public const string RuleName = nameof(RegulatoryRestrictionRule);

    public string Name => RuleName;

    public FilterResult Evaluate(AssignmentContext context, Candidato candidato)
    {
        var fecha = context.Solicitud.FechaServicio;

        if (!candidato.Conductor.LicenciaVigenteEn(fecha))
            return FilterResult.Fail($"La licencia del conductor venció el {candidato.Conductor.VencimientoLicencia:yyyy-MM-dd}.");
        if (!candidato.Vehiculo.DocumentacionVigenteEn(fecha))
            return FilterResult.Fail($"El vehículo {candidato.Vehiculo.Placa} no tiene RTM y SOAT vigentes.");

        return FilterResult.Pass("Conductor habilitado y documentación del vehículo vigente.");
    }
}
