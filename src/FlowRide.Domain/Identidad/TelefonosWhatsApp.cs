using System.Text.RegularExpressions;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Identidad;

/// <summary>
/// Teléfono de WhatsApp en formato internacional E.164 (<c>+573001112233</c>). Es la identidad del pasajero y del
/// conductor en el canal (ADR-0008): el bot busca al usuario por el número exacto que envía el proveedor.
/// </summary>
public static partial class TelefonosWhatsApp
{
    /// <summary>Quita espacios, guiones, puntos y paréntesis y exige <c>+</c> seguido de 8 a 15 dígitos.</summary>
    public static string Normalizar(string? telefono)
    {
        var limpio = Separadores().Replace(telefono ?? string.Empty, string.Empty);
        return FormatoE164().IsMatch(limpio)
            ? limpio
            : throw new FlowRideExcepcion("telefonoWhatsApp debe estar en formato internacional, por ejemplo +573001112233.");
    }

    [GeneratedRegex(@"[\s\-\.\(\)]")]
    private static partial Regex Separadores();

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$")]
    private static partial Regex FormatoE164();
}
