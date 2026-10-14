using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.UnitTests.Domain.Entidades;

/// <summary>Datos de prueba deterministas para las entidades del dominio.</summary>
internal static class Datos
{
    public static readonly DateTimeOffset Ahora = new(2026, 10, 14, 8, 0, 0, TimeSpan.FromHours(-5));
    public static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora.DateTime);

    public static Guid Id(int n) => new($"00000000-0000-0000-0000-{n:D12}");

    public static readonly Guid RolId = Id(1);
    public static readonly Guid EmpresaId = Id(2);
    public static readonly Guid OperadorId = Id(3);

    public static FranjaHoraria Franja(int desdeHoras, int hastaHoras) =>
        new(Ahora.AddHours(desdeHoras), Ahora.AddHours(hastaHoras));

    public static Pasajero Pasajero() =>
        new(Id(10), "Ana Ruiz", "+573001112233", RolId, Ahora.AddDays(-30), "TRIP-001", EmpresaId, "ana@aerolinea.co");

    public static Conductor Conductor(EstadoConductor estado = EstadoConductor.Activo) =>
        new(Id(20), "Luis Pérez", "+573004445566", RolId, Ahora.AddYears(-1), "LIC-20", "C2", Hoy.AddYears(1),
            estado: estado);

    public static SolicitudViaje Solicitud(bool urgente = false) =>
        new(Id(30), Id(10), "Aeropuerto JMC", "Hotel Centro", Ahora.AddHours(4), TimeSpan.FromMinutes(45),
            3, CanalSolicitud.WhatsApp, Ahora, urgente, esPersonal: false, empresaClienteId: EmpresaId);

    public static Asignacion Asignacion(SolicitudViaje? solicitud = null, OrigenAsignacion origen = OrigenAsignacion.Automatica)
    {
        solicitud ??= Solicitud();
        return new Asignacion(Id(40), solicitud.Id, Id(20), Id(50), solicitud.Franja(), origen, Ahora,
            decisionId: origen == OrigenAsignacion.Automatica ? Id(60) : null);
    }

    public static Viaje ViajeEnCurso()
    {
        var asignacion = Asignacion();
        asignacion.Aceptar();
        return asignacion.Iniciar(Id(70), Ahora.AddHours(5), Ahora.AddHours(4));
    }
}
