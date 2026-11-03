using System.Text.RegularExpressions;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Plataforma;

/// <summary>
/// Cómo llega el borde común al stack de un operador (ADR-0012): su subdominio del Panel Web y la API, la URL interna
/// de su stack y el <c>phone_number_id</c> de su número de WhatsApp (ADR-0008). Vive en el catálogo de plataforma, que
/// solo guarda operadores y ruteo; la registra la herramienta de aprovisionamiento.
/// </summary>
public sealed partial class RutaOperador
{
    public RutaOperador(string operadorId, string subdominio, string urlStack, string? phoneNumberId, DateTimeOffset actualizadaEn)
    {
        if (!Operador.EsIdValido(operadorId))
            throw new FlowRideExcepcion($"El operador '{operadorId}' no es un Id válido.");
        OperadorId = operadorId;
        Actualizar(subdominio, urlStack, phoneNumberId, actualizadaEn);
    }

    public string OperadorId { get; }

    /// <summary>Primera etiqueta del host: <c>{subdominio}.{dominio del borde}</c> lleva únicamente a este stack (OPLOG-SEG-02).</summary>
    public string Subdominio { get; private set; } = string.Empty;

    /// <summary>URL base del stack, sin ruta (por ejemplo <c>http://localhost:5237</c> o la dirección interna en Azure).</summary>
    public string UrlStack { get; private set; } = string.Empty;

    /// <summary>Id del número de WhatsApp del operador en Meta; null si todavía no tiene canal.</summary>
    public string? PhoneNumberId { get; private set; }

    public DateTimeOffset ActualizadaEn { get; private set; }

    public void Actualizar(string subdominio, string urlStack, string? phoneNumberId, DateTimeOffset ahora)
    {
        var sub = subdominio?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PatronSubdominio().IsMatch(sub))
            throw new FlowRideExcepcion("El subdominio debe tener de 1 a 63 letras minúsculas, números o guiones, sin guion al inicio ni al final.");

        if (!Uri.TryCreate(urlStack?.Trim(), UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https")
            || url.AbsolutePath != "/" || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment) || !string.IsNullOrEmpty(url.UserInfo))
            throw new FlowRideExcepcion("La URL del stack debe ser http(s)://host[:puerto], sin ruta, consulta ni credenciales.");

        var numero = string.IsNullOrWhiteSpace(phoneNumberId) ? null : phoneNumberId.Trim();
        if (numero is not null && !PatronNumero().IsMatch(numero))
            throw new FlowRideExcepcion("El phone_number_id de Meta es numérico (de 5 a 30 dígitos).");

        Subdominio = sub;
        UrlStack = url.GetLeftPart(UriPartial.Authority);
        PhoneNumberId = numero;
        ActualizadaEn = ahora;
    }

    /// <summary>Host público del operador en el borde (<c>tenant-a.dev.localhost</c> en local).</summary>
    public string Host(string dominioBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominioBase);
        return $"{Subdominio}.{dominioBase.Trim().TrimStart('.').ToLowerInvariant()}";
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex PatronSubdominio();

    [GeneratedRegex("^[0-9]{5,30}$")]
    private static partial Regex PatronNumero();
}

/// <summary>
/// Tabla de ruteo vigente del borde: una foto inmutable de las rutas de los operadores activos. El borde la reemplaza
/// entera al recargarla, así que una petición nunca ve una tabla a medias.
/// </summary>
public sealed class TablaRutas
{
    private readonly Dictionary<string, RutaOperador> _porNumero;

    public TablaRutas(IEnumerable<RutaOperador> rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);
        Rutas = rutas.OrderBy(r => r.OperadorId, StringComparer.Ordinal).ToList();
        if (Rutas.GroupBy(r => r.Subdominio, StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new FlowRideExcepcion("Dos operadores no pueden compartir subdominio.");
        _porNumero = new Dictionary<string, RutaOperador>(StringComparer.Ordinal);
        foreach (var ruta in Rutas.Where(r => r.PhoneNumberId is not null))
            if (!_porNumero.TryAdd(ruta.PhoneNumberId!, ruta))
                throw new FlowRideExcepcion($"El phone_number_id {ruta.PhoneNumberId} está en dos operadores.");
    }

    public static TablaRutas Vacia { get; } = new([]);

    public IReadOnlyList<RutaOperador> Rutas { get; }

    public RutaOperador? PorNumero(string phoneNumberId) => _porNumero.GetValueOrDefault(phoneNumberId);
}
