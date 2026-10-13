namespace FlowRide.Domain.Assignment.Rules.Filtering;

/// <summary>La hora de servicio de la solicitud cae dentro de un turno vigente del conductor.</summary>
public sealed class ShiftCompatibilityRule : IFilteringRule
{
    public const string RuleName = nameof(ShiftCompatibilityRule);

    public string Name => RuleName;

    public FilterResult Evaluate(AssignmentContext context, Candidato candidato)
    {
        var turno = candidato.Turno;
        var hora = context.Solicitud.FechaHoraServicio;

        if (!turno.EstaVigente)
            return FilterResult.Fail($"El turno no está vigente (estado {turno.Estado}).");
        if (!turno.Cubre(hora))
            return FilterResult.Fail($"La hora de servicio {hora:O} está fuera del turno [{turno.Inicio:O}, {turno.Fin:O}).");

        return FilterResult.Pass("La hora de servicio cae dentro del turno.");
    }
}
