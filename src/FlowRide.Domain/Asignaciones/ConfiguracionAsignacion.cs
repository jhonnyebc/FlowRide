using System.Globalization;
using System.Text.Json;
using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Assignment.Rules.Filtering;
using FlowRide.Domain.Assignment.Rules.Priority;
using FlowRide.Domain.Assignment.Rules.Scoring;
using FlowRide.Domain.Assignment.Rules.TieBreaking;

namespace FlowRide.Domain.Asignaciones;

public enum EstadoConfiguracion
{
    Borrador,
    Activa,
    Archivada
}

/// <summary>Una regla del catálogo tal como la configura el operador (motor.regla_configurada).</summary>
public sealed class ReglaConfigurada
{
    public ReglaConfigurada(Guid id, Guid configuracionId, string codigoRegla, RuleKind tipo, bool habilitada, int orden, decimal peso,
        IReadOnlyDictionary<string, string>? parametros = null)
    {
        Id = id;
        ConfiguracionId = configuracionId;
        CodigoRegla = codigoRegla;
        Tipo = tipo;
        Habilitada = habilitada;
        Orden = orden;
        Peso = peso;
        Parametros = parametros is null || parametros.Count == 0
            ? null
            : JsonSerializer.Serialize(new SortedDictionary<string, string>(parametros.ToDictionary(), StringComparer.Ordinal));
    }

#pragma warning disable CS8618 // Solo para EF Core.
    private ReglaConfigurada()
    {
    }
#pragma warning restore CS8618

    public Guid Id { get; private set; }
    public Guid ConfiguracionId { get; private set; }
    public string CodigoRegla { get; private set; }
    public RuleKind Tipo { get; private set; }
    public bool Habilitada { get; private set; }
    public int Orden { get; private set; }
    public decimal Peso { get; private set; }

    /// <summary>Parámetros propios de la regla, como JSON de texto (motor.regla_configurada.parametros, jsonb).</summary>
    public string? Parametros { get; private set; }

    public IReadOnlyDictionary<string, string> LeerParametros() =>
        Parametros is null ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(Parametros)!;
}

/// <summary>
/// Configuración versionada del motor del operador (RF-21, ADR-0009). Se crea como borrador, se valida y se activa; solo
/// una está activa a la vez. Volver a activar una versión anterior es el rollback.
/// </summary>
public sealed class ConfiguracionAsignacion
{
    private readonly List<ReglaConfigurada> _reglas = [];

    public ConfiguracionAsignacion(Guid id, int version, IEnumerable<ReglaPedida> reglas, IReadOnlyCollection<IAssignmentRule> catalogo,
        Guid? creadaPorId, DateTimeOffset creadaEn)
    {
        ArgumentNullException.ThrowIfNull(reglas);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);

        Id = id;
        Version = version;
        Estado = EstadoConfiguracion.Borrador;
        CreadaPorId = creadaPorId;
        CreadaEn = creadaEn;
        var tipos = catalogo.ToDictionary(regla => regla.Name, regla => regla.Kind, StringComparer.Ordinal);
        foreach (var regla in reglas)
        {
            _reglas.Add(new ReglaConfigurada(Guid.CreateVersion7(creadaEn), id, regla.Codigo,
                tipos.GetValueOrDefault(regla.Codigo, RuleKind.Filtering), regla.Habilitada, regla.Orden, regla.Peso, regla.Parametros));
        }
    }

#pragma warning disable CS8618 // Solo para EF Core.
    private ConfiguracionAsignacion()
    {
    }
#pragma warning restore CS8618

    public Guid Id { get; private set; }
    public int Version { get; private set; }
    public EstadoConfiguracion Estado { get; private set; }
    public Guid? CreadaPorId { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset? ActivadaEn { get; private set; }
    public IReadOnlyList<ReglaConfigurada> Reglas => _reglas;

    public void Activar(DateTimeOffset ahora)
    {
        if (Estado == EstadoConfiguracion.Activa)
            throw new InvalidOperationException($"La configuración v{Version} ya está activa.");
        Estado = EstadoConfiguracion.Activa;
        ActivadaEn = ahora;
    }

    public void Archivar()
    {
        if (Estado != EstadoConfiguracion.Activa)
            throw new InvalidOperationException($"Solo se archiva la configuración activa (v{Version} está {Estado}).");
        Estado = EstadoConfiguracion.Archivada;
    }

    /// <summary>La configuración tal como la evalúa el motor.</summary>
    public AssignmentConfiguration ComoConfiguracionDelMotor(string tenantId) =>
        new(tenantId, Version, _reglas.Select(regla => new RuleSetting(regla.CodigoRegla, regla.Habilitada, regla.Orden, regla.Peso)));

    public int? ParametroEntero(string codigoRegla, string clave) =>
        _reglas.FirstOrDefault(regla => regla.CodigoRegla == codigoRegla)?.LeerParametros().GetValueOrDefault(clave) is { } valor
        && int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : null;
}

/// <summary>Una regla pedida para una configuración nueva.</summary>
public sealed record ReglaPedida(string Codigo, bool Habilitada, int Orden, decimal Peso, IReadOnlyDictionary<string, string>? Parametros = null);

/// <summary>
/// Configuración con la que nace cada Silo (v1, activa), sembrada por la migración: las cuatro eliminatorias obligatorias,
/// la prioridad de servicio crítico, el balance de carga (ventana de 7 días) y el desempate estable, con peso 1.
/// </summary>
public static class ConfiguracionInicial
{
    public static readonly Guid Id = new("b1000000-0000-0000-0000-000000000001");
    public const int Version = 1;
    public const string ParametroVentanaDias = "ventanaDias";
    public const int VentanaDiasPorDefecto = 7;

    public static IReadOnlyList<ReglaPedida> Reglas { get; } =
    [
        new(AvailabilityRule.RuleName, true, 1, 1m),
        new(ShiftCompatibilityRule.RuleName, true, 2, 1m),
        new(RegulatoryRestrictionRule.RuleName, true, 3, 1m),
        new(ServiceCapabilityRule.RuleName, true, 4, 1m),
        new(CriticalServicePriorityRule.RuleName, true, 1, 1m),
        new(WorkloadBalanceRule.RuleName, true, 1, 1m,
            new Dictionary<string, string> { [ParametroVentanaDias] = VentanaDiasPorDefecto.ToString(CultureInfo.InvariantCulture) }),
        new(StableTieBreaker.RuleName, true, 1, 1m)
    ];
}
