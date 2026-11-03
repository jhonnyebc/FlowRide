namespace FlowRide.Domain.Assignment.Rules;

/// <summary>Regla del catálogo. Las reglas son independientes entre sí; solo AssignmentEngine las orquesta.</summary>
public interface IAssignmentRule
{
    /// <summary>Nombre con el que la configuración del tenant referencia la regla.</summary>
    string Name { get; }

    RuleKind Kind { get; }

    /// <summary>Versión de la lógica de la regla: sube cuando cambia su comportamiento y queda en el decision log.</summary>
    int Version => 1;

    /// <summary>Regla obligatoria (ADR-0009): toda configuración debe tenerla habilitada.</summary>
    bool Obligatoria => false;

    /// <summary>Reglas que deben estar habilitadas para que esta tenga sentido. Sin ciclos.</summary>
    IReadOnlyCollection<string> Dependencias => [];
}
