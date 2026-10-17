using System.Text.RegularExpressions;

namespace FlowRide.Domain.Plataforma;

/// <summary>
/// Operador de transporte registrado como tenant de FlowRide (RF-03). Vive en el catálogo de plataforma,
/// que no guarda datos operacionales: solo quién es el operador y cómo llegar a su Silo (ADR-0002).
/// </summary>
public sealed partial class Operador
{
    public Operador(string id, string razonSocial, string nit, string correoContacto, DateTimeOffset creadoEn)
    {
        if (!EsIdValido(id))
            throw new ArgumentException(
                "El Id del operador debe estar en minúsculas, con letras, números y guiones (por ejemplo 'tenant-a').",
                nameof(id));

        Id = id;
        CreadoEn = creadoEn;
        Estado = EstadoOperador.Aprovisionando;
        ActualizarDatos(razonSocial, nit, correoContacto);
    }

    /// <summary>Identificador estable del tenant; es el claim <c>tenant_id</c> y la configuración <c>FlowRide:TenantId</c> del stack.</summary>
    public string Id { get; }

    public string RazonSocial { get; private set; } = string.Empty;
    public string Nit { get; private set; } = string.Empty;
    public string CorreoContacto { get; private set; } = string.Empty;
    public EstadoOperador Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; }

    /// <summary>Nombre de la base de datos y del rol propios del Silo del operador.</summary>
    public string NombreSilo => NombreSiloDe(Id);

    /// <summary>Ámbito de secretos del operador (Key Vault en la nube; user-secrets en local, ADR-0014).</summary>
    public string AmbitoSecretos => AmbitoSecretosDe(Id);

    public static bool EsIdValido(string? id) => id is not null && id.Length <= 40 && PatronId().IsMatch(id);

    public static string NombreSiloDe(string id) => "flowride_" + id.Replace('-', '_');

    public static string AmbitoSecretosDe(string id) => "flowride-" + id;

    public void ActualizarDatos(string razonSocial, string nit, string correoContacto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(razonSocial);
        ArgumentException.ThrowIfNullOrWhiteSpace(nit);
        ArgumentException.ThrowIfNullOrWhiteSpace(correoContacto);

        RazonSocial = razonSocial.Trim();
        Nit = nit.Trim();
        CorreoContacto = correoContacto.Trim();
    }

    /// <summary>El Silo quedó aprovisionado y migrado: el operador puede operar.</summary>
    public void Activar()
    {
        if (Estado == EstadoOperador.Suspendido)
            throw new InvalidOperationException("Un operador suspendido se reactiva de forma explícita, no al reaprovisionarlo.");
        Estado = EstadoOperador.Activo;
    }

    public void Suspender() => Estado = EstadoOperador.Suspendido;

    public void Reactivar()
    {
        if (Estado != EstadoOperador.Suspendido)
            throw new InvalidOperationException($"Solo se reactiva un operador suspendido; está en estado {Estado}.");
        Estado = EstadoOperador.Activo;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex PatronId();
}
