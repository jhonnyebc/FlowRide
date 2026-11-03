using System.Text.Json;
using System.Text.RegularExpressions;

namespace FlowRide.Application.Notificaciones.Catalogo;

/// <summary>Un texto del catálogo ya armado: el texto y los valores de sus variables en orden (para la plantilla de Meta).</summary>
public sealed record MensajeArmado(string Clave, string Texto, IReadOnlyList<string> Parametros)
{
    /// <summary>Las alertas se escriben como <c>título|detalle</c>.</summary>
    public (string Titulo, string Detalle) TituloYDetalle()
    {
        var partes = Texto.Split('|', 2);
        return partes.Length == 2 ? (partes[0], partes[1]) : (Texto, Texto);
    }
}

/// <summary>
/// Catálogo de mensajes (ADR-0016): cada texto tiene una clave estable y sus variables en orden, en un recurso versionado
/// junto al código (<c>mensajes.es-CO.json</c>). Agregar un idioma es agregar un archivo.
/// </summary>
public sealed partial class CatalogoMensajes
{
    public const string IdiomaPorDefecto = "es-CO";

    private sealed record Entrada(string Texto, IReadOnlyList<string> Variables);

    private readonly IReadOnlyDictionary<string, Entrada> _entradas;

    private CatalogoMensajes(IReadOnlyDictionary<string, Entrada> entradas) => _entradas = entradas;

    public IReadOnlyCollection<string> Claves => _entradas.Keys.ToList();

    /// <summary>El catálogo del idioma, embebido en el ensamblado de Application.</summary>
    public static CatalogoMensajes Cargar(string idioma = IdiomaPorDefecto)
    {
        var recurso = $"FlowRide.Mensajes.mensajes.{idioma}.json";
        using var flujo = typeof(CatalogoMensajes).Assembly.GetManifestResourceStream(recurso)
                          ?? throw new InvalidOperationException($"No existe el catálogo de mensajes '{recurso}'.");
        using var documento = JsonDocument.Parse(flujo);
        var entradas = documento.RootElement.EnumerateObject().ToDictionary(
            propiedad => propiedad.Name,
            propiedad => new Entrada(
                propiedad.Value.GetProperty("texto").GetString()!,
                propiedad.Value.GetProperty("variables").EnumerateArray().Select(v => v.GetString()!).ToList()),
            StringComparer.Ordinal);
        return new CatalogoMensajes(entradas);
    }

    public IReadOnlyList<string> Variables(string clave) => Buscar(clave).Variables;

    /// <summary>Reemplaza <c>{variable}</c> por su valor. Falla si falta alguna variable declarada para la clave.</summary>
    public MensajeArmado Armar(string clave, IReadOnlyDictionary<string, string> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        var entrada = Buscar(clave);
        var faltantes = entrada.Variables.Where(v => !valores.ContainsKey(v)).ToList();
        if (faltantes.Count > 0)
            throw new InvalidOperationException($"Faltan variables para el mensaje '{clave}': {string.Join(", ", faltantes)}.");

        var texto = Variable().Replace(entrada.Texto, m => valores.TryGetValue(m.Groups[1].Value, out var valor) ? valor : m.Value);
        return new MensajeArmado(clave, texto, entrada.Variables.Select(v => valores[v]).ToList());
    }

    private Entrada Buscar(string clave) =>
        _entradas.TryGetValue(clave, out var entrada) ? entrada : throw new InvalidOperationException($"El mensaje '{clave}' no está en el catálogo.");

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex Variable();
}
