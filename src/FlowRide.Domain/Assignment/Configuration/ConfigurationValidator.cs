using FlowRide.Domain.Assignment.Rules;

namespace FlowRide.Domain.Assignment.Configuration;

/// <summary>
/// Valida una configuración antes de activarla (ADR-0009, "Validación de configuración"): reglas duplicadas o
/// inexistentes, pesos fuera de rango, precedencias inválidas, reglas obligatorias deshabilitadas, dependencias
/// incompatibles, ciclos de dependencia y configuraciones incompletas.
/// </summary>
public sealed class ConfigurationValidator
{
    public ValidationResult Validate(AssignmentConfiguration configuration, IReadOnlyCollection<IAssignmentRule> catalog)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(catalog);

        var reglas = catalog.ToDictionary(rule => rule.Name, StringComparer.Ordinal);
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
            if (!reglas.ContainsKey(setting.Name))
                errors.Add($"La regla '{setting.Name}' no existe en el catálogo.");
            if (setting.Weight is < 0m or > 1m)
                errors.Add($"El peso de la regla '{setting.Name}' ({setting.Weight}) está fuera de [0, 1].");
            if (setting.Order <= 0)
                errors.Add($"El orden de la regla '{setting.Name}' debe ser mayor que cero.");
        }

        // Orden único por tipo: la ejecución no debe depender del nombre (UQ configuracion_id, tipo, orden).
        foreach (var clash in distinct
                     .Where(setting => reglas.ContainsKey(setting.Name))
                     .GroupBy(setting => (reglas[setting.Name].Kind, setting.Order))
                     .Where(group => group.Count() > 1))
        {
            var names = string.Join(", ", clash.Select(setting => $"'{setting.Name}'"));
            errors.Add($"Las reglas {names} de tipo {clash.Key.Kind} comparten el orden {clash.Key.Order}.");
        }

        var habilitadas = distinct.Where(setting => setting.Enabled).Select(setting => setting.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var obligatoria in reglas.Values.Where(rule => rule.Obligatoria).OrderBy(rule => rule.Name, StringComparer.Ordinal))
        {
            if (!habilitadas.Contains(obligatoria.Name))
                errors.Add($"La regla obligatoria '{obligatoria.Name}' debe estar habilitada.");
        }

        foreach (var nombre in habilitadas.Where(reglas.ContainsKey).Order(StringComparer.Ordinal))
        {
            foreach (var dependencia in reglas[nombre].Dependencias.Order(StringComparer.Ordinal))
            {
                if (!habilitadas.Contains(dependencia))
                    errors.Add($"La regla '{nombre}' necesita la regla '{dependencia}' habilitada.");
            }
        }

        if (BuscarCiclo(reglas) is { } ciclo)
            errors.Add($"Hay un ciclo de dependencias entre reglas: {string.Join(" -> ", ciclo)}.");

        var hasFilteringRule = habilitadas.Any(name => reglas.TryGetValue(name, out var rule) && rule.Kind == RuleKind.Filtering);
        if (!hasFilteringRule)
            errors.Add("La configuración debe tener al menos una regla eliminatoria habilitada.");

        return new ValidationResult(errors);
    }

    /// <summary>Primer ciclo del grafo de dependencias del catálogo, en orden determinista; null si no hay.</summary>
    private static List<string>? BuscarCiclo(IReadOnlyDictionary<string, IAssignmentRule> reglas)
    {
        var visitadas = new HashSet<string>(StringComparer.Ordinal);
        var enCurso = new List<string>();

        List<string>? Visitar(string regla)
        {
            var posicion = enCurso.IndexOf(regla);
            if (posicion >= 0) return [.. enCurso[posicion..], regla];
            if (!visitadas.Add(regla)) return null;

            enCurso.Add(regla);
            var dependencias = reglas.TryGetValue(regla, out var definicion) ? definicion.Dependencias : [];
            foreach (var dependencia in dependencias.Order(StringComparer.Ordinal))
            {
                if (Visitar(dependencia) is { } ciclo) return ciclo;
            }
            enCurso.RemoveAt(enCurso.Count - 1);
            return null;
        }

        foreach (var regla in reglas.Keys.Order(StringComparer.Ordinal))
        {
            if (Visitar(regla) is { } ciclo) return ciclo;
        }
        return null;
    }
}
