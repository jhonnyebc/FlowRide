using FlowRide.Domain.Comun;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Application.Comun;

/// <summary>
/// Hora de un evento futuro tal como llega en una petición: hora local sin desfase y, opcionalmente, el nombre IANA de la
/// zona; sin zona se usa la del operador. Un valor con desfase o en UTC se rechaza (la zona es el nombre, no el desfase).
/// </summary>
public static class HorasLocales
{
    public static FechaHoraLocal? Opcional(DateTime? hora, string? zona, ZonaHorariaOperador operador)
    {
        ArgumentNullException.ThrowIfNull(operador);
        return hora is { } valor ? new FechaHoraLocal(valor, string.IsNullOrWhiteSpace(zona) ? operador.Zona.Id : zona) : null;
    }
}
