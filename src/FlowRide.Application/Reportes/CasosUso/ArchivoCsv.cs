using System.Globalization;
using System.Text;
using FlowRide.Domain.Comun;

namespace FlowRide.Application.Reportes.CasosUso;

/// <summary>
/// CSV para Excel en español (decisión del 2026-10-19): separador <c>;</c>, decimales con coma, UTF-8 con BOM y fechas
/// en la hora local del operador. Los textos que empiezan como una fórmula (<c>= + - @</c>) llevan un apóstrofo delante,
/// para que la hoja de cálculo no los ejecute (inyección CSV).
/// </summary>
internal sealed class ArchivoCsv
{
    public const char Separador = ';';
    public const string TipoContenido = "text/csv; charset=utf-8";

    private static readonly char[] Especiales = [Separador, '"', '\r', '\n'];
    private static readonly char[] InicioFormula = ['=', '+', '-', '@', '\t', '\r'];

    private readonly StringBuilder _texto = new();
    private readonly ZonaHorariaOperador _zona;

    public ArchivoCsv(ZonaHorariaOperador zona, params string[] encabezados)
    {
        _zona = zona ?? throw new ArgumentNullException(nameof(zona));
        Escribir(encabezados);
    }

    /// <summary>Filas de datos escritas (sin el encabezado).</summary>
    public int Filas { get; private set; }

    public ArchivoCsv Fila(params object?[] valores)
    {
        Escribir(valores);
        Filas++;
        return this;
    }

    public byte[] Contenido() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_texto.ToString())];

    private void Escribir(IEnumerable<object?> valores)
    {
        _texto.AppendJoin(Separador, valores.Select(Celda));
        _texto.Append("\r\n");
    }

    private string Celda(object? valor) => valor switch
    {
        null => string.Empty,
        string texto => Texto(texto),
        bool b => b ? "Sí" : "No",
        DateTimeOffset instante => _zona.Local(instante).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal d => d.ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ','),
        int n => n.ToString(CultureInfo.InvariantCulture),
        Guid id => id.ToString(),
        Enum e => e.ToString(),
        IFormattable f => Texto(f.ToString(null, CultureInfo.InvariantCulture)),
        _ => Texto(valor.ToString() ?? string.Empty)
    };

    internal static string Texto(string texto)
    {
        if (texto.Length > 0 && InicioFormula.Contains(texto[0]))
            texto = "'" + texto;
        return texto.IndexOfAny(Especiales) < 0 ? texto : $"\"{texto.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
