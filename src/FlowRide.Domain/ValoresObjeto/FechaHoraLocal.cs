using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.ValoresObjeto;

/// <summary>
/// Fecha y hora de un evento futuro en un lugar: la hora local tal como la pidió la persona y el <b>nombre</b> IANA de la
/// zona (por ejemplo <c>America/Bogota</c>), nunca un desfase como <c>-05:00</c>, que es solo el resultado de una zona en un
/// momento. El instante UTC se calcula cada vez con las reglas vigentes de la zona, así que si un gobierno cambia el
/// horario de verano, el evento sigue cayendo a la hora local acordada. Lo que ya pasó se guarda como instante UTC.
/// </summary>
public sealed record FechaHoraLocal
{
    public FechaHoraLocal(DateTime hora, string zona)
    {
        if (hora.Kind != DateTimeKind.Unspecified)
            throw new FlowRideExcepcion("La hora local va sin desfase ni zona (por ejemplo 2026-10-21T08:00); la zona se indica por su nombre.");
        Zona = ZonaValida(zona);
        Hora = new DateTime(hora.Ticks - hora.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Unspecified);
        if (Reglas.IsInvalidTime(Hora))
            throw new FlowRideExcepcion($"La hora {Hora:yyyy-MM-dd HH:mm} no existe en {Zona}: el reloj salta esa hora por el cambio de horario.");
    }

    /// <summary>Hora local, al minuto y sin zona (<see cref="DateTimeKind.Unspecified"/>).</summary>
    public DateTime Hora { get; }

    /// <summary>Nombre IANA de la zona; carga las reglas de antes, las de ahora y las que vengan.</summary>
    public string Zona { get; }

    public DateOnly Fecha => DateOnly.FromDateTime(Hora);

    private TimeZoneInfo Reglas => TimeZoneInfo.FindSystemTimeZoneById(Zona);

    /// <summary>
    /// El instante con las reglas vigentes hoy. Si la hora se repite (el reloj se atrasa), se toma la primera vez que
    /// ocurre; si por un cambio de reglas dejó de existir, la siguiente hora válida.
    /// </summary>
    public DateTimeOffset Instante()
    {
        var reglas = Reglas;
        var hora = Hora;
        while (reglas.IsInvalidTime(hora))
            hora = hora.AddMinutes(1);
        var desfase = reglas.IsAmbiguousTime(hora) ? reglas.GetAmbiguousTimeOffsets(hora).Max() : reglas.GetUtcOffset(hora);
        return new DateTimeOffset(hora, desfase).ToUniversalTime();
    }

    public FechaHoraLocal Mas(TimeSpan duracion) => new(Hora + duracion, Zona);

    /// <summary>La hora local de un instante en una zona (para pasar un instante ya conocido a hora local).</summary>
    public static FechaHoraLocal De(DateTimeOffset instante, string zona) =>
        new(DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(ZonaValida(zona))).DateTime,
            DateTimeKind.Unspecified), zona);

    public override string ToString() => $"{Hora:yyyy-MM-dd HH:mm} ({Zona})";

    private static string ZonaValida(string zona)
    {
        var nombre = zona?.Trim() ?? string.Empty;
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(nombre, out var reglas) || !reglas.HasIanaId)
            throw new FlowRideExcepcion($"'{zona}' no es una zona horaria IANA (por ejemplo America/Bogota).");
        return nombre;
    }
}
