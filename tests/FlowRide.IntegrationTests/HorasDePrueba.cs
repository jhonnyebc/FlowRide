using FlowRide.Domain.Comun;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.IntegrationTests;

/// <summary>
/// Las pruebas razonan con instantes; los eventos futuros se guardan en hora local + zona. Estos atajos pasan un instante
/// a la hora local de la zona por defecto del producto (la de los stacks de prueba).
/// </summary>
internal static class HorasDePrueba
{
    public const string Zona = "America/Bogota";

    public static ZonaHorariaOperador ZonaOperador { get; } = new(TimeZoneInfo.FindSystemTimeZoneById(Zona));

    public static FechaHoraLocal Local(this DateTimeOffset instante) => FechaHoraLocal.De(instante, Zona);

    /// <summary>La hora local sin zona, como la envía un cliente de la API.</summary>
    public static DateTime HoraLocal(this DateTimeOffset instante) => instante.Local().Hora;

    public static Turno ProgramarTurno(this Conductor conductor, Guid turnoId, FranjaHoraria franja) =>
        conductor.ProgramarTurno(turnoId, franja.Inicio.Local(), franja.Fin.Local());

    public static Parada RegistrarParada(this Viaje viaje, Guid paradaId, string direccion, int orden, DateTimeOffset horaEstimada) =>
        viaje.RegistrarParada(paradaId, direccion, orden, horaEstimada.Local());
}
