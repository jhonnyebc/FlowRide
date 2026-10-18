namespace FlowRide.Domain.Comun;

/// <summary>
/// Zona horaria en la que opera el operador (configuración del stack, por defecto America/Bogota). Convierte la fecha y
/// la hora de los itinerarios y las franjas de los parámetros, que se expresan en hora local, a instantes.
/// </summary>
public sealed class ZonaHorariaOperador(TimeZoneInfo zona)
{
    public TimeZoneInfo Zona { get; } = zona ?? throw new ArgumentNullException(nameof(zona));

    public DateTimeOffset Instante(DateOnly fecha, TimeOnly hora)
    {
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zona.GetUtcOffset(local));
    }

    public DateTimeOffset Local(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Zona);
}
