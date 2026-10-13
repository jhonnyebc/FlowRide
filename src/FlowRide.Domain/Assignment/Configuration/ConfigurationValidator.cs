using FlowRide.Domain.Assignment.Rules;

namespace FlowRide.Domain.Assignment.Configuration;

public sealed class ConfigurationValidator
{
    public ValidationResult Validate(AssignmentConfiguration configuration, IReadOnlyCollection<IAssignmentRule> catalog)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(catalog);

        var kinds = catalog.ToDictionary(rule => rule.Name, rule => rule.Kind, StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var duplicated in configuration.Rules
                     .GroupBy(setting => setting.Name, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"La regla '{duplicated.Key}' está duplicada en la configuración.");
        }

        var distinct = configuration.Rules.DistinctBy(setting => setting.Name, StringComparer.Ordinal).ToArray();

        foreach (var setting in distinct)
        {
            if (!kinds.ContainsKey(setting.Name))
                errors.Add($"La regla '{setting.Name}' no existe en el catálogo.");
            if (setting.Weight is < 0m or > 1m)
                errors.Add($"El peso de la regla '{setting.Name}' ({setting.Weight}) está fuera de [0, 1].");
        }

        // Orden único por tipo: la ejecución no debe depender del nombre (UQ configuracion_id, tipo, orden).
        foreach (var clash in distinct
                     .Where(setting => kinds.ContainsKey(setting.Name))
                     .GroupBy(setting => (Kind: kinds[setting.Name], setting.Order))
                     .Where(group => group.Count() > 1))
        {
            var names = string.Join(", ", clash.Select(setting => $"'{setting.Name}'"));
            errors.Add($"Las reglas {names} de tipo {clash.Key.Kind} comparten el orden {clash.Key.Order}.");
        }

        var hasFilteringRule = distinct.Any(setting =>
            setting.Enabled
            && kinds.TryGetValue(setting.Name, out var kind)
            && kind == RuleKind.Filtering);
        if (!hasFilteringRule)
            errors.Add("La configuración debe tener al menos una regla eliminatoria habilitada.");

        return new ValidationResult(errors);
    }
}
