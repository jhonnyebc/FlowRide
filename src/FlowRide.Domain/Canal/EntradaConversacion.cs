using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FlowRide.Domain.Canal;

/// <summary>Interpretación de lo que escribe el usuario en el bot: opciones, fechas, números y palabras de control.</summary>
public static partial class EntradaConversacion
{
    private static readonly string[] PalabrasMenu = ["menu", "inicio", "hola", "salir", "cancelar", "volver"];

    /// <summary>Texto sin tildes, en minúscula y sin espacios sobrantes.</summary>
    public static string Normalizar(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder(limpio.Length);
        foreach (var caracter in limpio.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            sinTildes.Append(caracter);
        return Espacios().Replace(sinTildes.ToString().Normalize(NormalizationForm.FormC), " ");
    }

    /// <summary>
    /// Teléfono en formato E.164 (<c>+573001112233</c>), como se registran pasajeros y conductores. El proveedor lo envía
    /// solo con dígitos (<c>573001112233</c>).
    /// </summary>
    public static string TelefonoE164(string numero)
    {
        var digitos = new string((numero ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digitos.Length == 0 ? throw new ArgumentException("El teléfono no tiene dígitos.", nameof(numero)) : "+" + digitos;
    }

    /// <summary>¿El usuario pide volver al menú (o empezar)?</summary>
    public static bool PideMenu(string? texto) => PalabrasMenu.Contains(Normalizar(texto));

    /// <summary>Un número entero dentro del rango, o null.</summary>
    public static int? Numero(string? texto, int minimo, int maximo) =>
        int.TryParse(Normalizar(texto).TrimEnd('.'), NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
        && numero >= minimo && numero <= maximo
            ? numero
            : null;

    /// <summary>
    /// Fecha y hora local del operador: <c>dd/mm hh:mm</c>, <c>dd/mm/aaaa hh:mm</c>, <c>hoy hh:mm</c> o <c>mañana hh:mm</c>
    /// (también acepta "am"/"pm"). Sin año, toma el próximo día con esa fecha. Null si no se entiende.
    /// </summary>
    public static (DateOnly Fecha, TimeOnly Hora)? FechaHora(string? texto, DateOnly hoy)
    {
        var partes = PatronFechaHora().Match(Normalizar(texto));
        if (!partes.Success || Hora(partes.Groups["hora"].Value, partes.Groups["minuto"].Value, partes.Groups["ampm"].Value) is not { } hora)
            return null;

        var dia = partes.Groups["dia"].Value;
        if (dia == "hoy") return (hoy, hora);
        if (dia is "manana" or "mañana") return (hoy.AddDays(1), hora);

        var fecha = PatronFecha().Match(dia);
        if (!fecha.Success
            || !int.TryParse(fecha.Groups["d"].Value, CultureInfo.InvariantCulture, out var d)
            || !int.TryParse(fecha.Groups["m"].Value, CultureInfo.InvariantCulture, out var m))
            return null;

        var anio = fecha.Groups["a"].Success ? int.Parse(fecha.Groups["a"].Value, CultureInfo.InvariantCulture) : hoy.Year;
        if (anio < 100) anio += 2000;
        if (m is < 1 or > 12 || d < 1 || d > DateTime.DaysInMonth(anio, m))
            return null;

        var resultado = new DateOnly(anio, m, d);
        if (!fecha.Groups["a"].Success && resultado < hoy)
            resultado = resultado.AddYears(1);
        return (resultado, hora);
    }

    private static TimeOnly? Hora(string hora, string minuto, string ampm)
    {
        var h = int.Parse(hora, CultureInfo.InvariantCulture);
        var min = string.IsNullOrEmpty(minuto) ? 0 : int.Parse(minuto, CultureInfo.InvariantCulture);
        if (ampm.StartsWith('p') && h < 12) h += 12;
        if (ampm.StartsWith('a') && h == 12) h = 0;
        return h is >= 0 and < 24 && min is >= 0 and < 60 ? new TimeOnly(h, min) : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    [GeneratedRegex(@"^(?<dia>hoy|manana|mañana|\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?)\s+(?:a las\s+)?(?<hora>\d{1,2})(?::(?<minuto>\d{2}))?\s*(?<ampm>am|pm|a\.\s?m\.|p\.\s?m\.)?$")]
    private static partial Regex PatronFechaHora();

    [GeneratedRegex(@"^(?<d>\d{1,2})[/-](?<m>\d{1,2})(?:[/-](?<a>\d{2,4}))?$")]
    private static partial Regex PatronFecha();
}
