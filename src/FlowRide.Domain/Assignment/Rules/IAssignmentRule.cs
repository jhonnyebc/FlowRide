namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Regla del catálogo. Las reglas son independientes entre sí; solo AssignmentEngine las orquesta.</summary>
public interface IAssignmentRule
{
    /// <summary>Nombre con el que la configuración del tenant referencia la regla.</summary>
    string Name { get; }

    RuleKind Kind { get; }
}
