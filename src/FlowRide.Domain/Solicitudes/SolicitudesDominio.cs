using FlowRide.Domain.Entidades;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Solicitudes;

/// <summary>Reglas de las solicitudes que consultan el Silo (RF-09, RF-29, RF-32).</summary>
public sealed class SolicitudDominio(ISolicitudRepositorio solicitudes, ParametrosDominio parametros)
{
    public async Task<SolicitudViaje> ValidarExistaAsync(Guid solicitudId, CancellationToken ct) =>
        await solicitudes.ConsultarPorIdAsync(solicitudId, ct)
        ?? throw new FlowRideExcepcion($"La solicitud {solicitudId} no existe.");

    /// <summary>Un pasajero no tiene dos solicitudes activas en franjas que se solapan (evita duplicados).</summary>
    public async Task ValidarSinSolapamientoAsync(Guid pasajeroId, FranjaHoraria franja, Guid? solicitudId, CancellationToken ct)
    {
        if (await solicitudes.ExisteActivaQueSeSolapaAsync(pasajeroId, franja, solicitudId, ct))
            throw new FlowRideExcepcion("El pasajero ya tiene una solicitud activa en ese horario.");
    }

    /// <summary>
    /// Ventana para modificar o cancelar (parámetro <c>solicitud.ventana_cancelacion_minutos</c>): hasta N minutos antes
    /// del servicio. El Operador logístico puede cancelar después por una contingencia, siempre con motivo.
    /// </summary>
    public async Task ValidarDentroDeVentanaAsync(SolicitudViaje solicitud, DateTimeOffset ahora, bool puedeSaltarVentana, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (puedeSaltarVentana)
            return;

        var minutos = await parametros.EnteroAsync(CatalogoParametros.VentanaCancelacionMinutos, ct);
        if (ahora > solicitud.FechaHoraServicio.AddMinutes(-minutos))
            throw new FlowRideExcepcion(
                $"Ya no se puede cambiar la solicitud: el plazo vence {minutos} minutos antes del servicio.");
    }

    /// <summary>RF-29: el servicio es en el futuro y los datos mínimos tienen sentido.</summary>
    public static void ValidarDatosMinimos(DateTimeOffset fechaHoraServicio, TimeSpan duracion, int cantidadPasajeros, DateTimeOffset ahora)
    {
        if (fechaHoraServicio <= ahora)
            throw new FlowRideExcepcion("La fecha y hora del servicio debe ser futura.");
        if (duracion <= TimeSpan.Zero || duracion > TimeSpan.FromHours(24))
            throw new FlowRideExcepcion("La duración estimada debe estar entre 1 minuto y 24 horas.");
        if (cantidadPasajeros <= 0)
            throw new FlowRideExcepcion("La cantidad de pasajeros debe ser mayor que cero.");
    }

    /// <summary>Solo un pasajero activo de una empresa cliente activa solicita viajes.</summary>
    public static void ValidarPuedeSolicitar(Pasajero pasajero, EmpresaCliente empresa)
    {
        ArgumentNullException.ThrowIfNull(pasajero);
        ArgumentNullException.ThrowIfNull(empresa);
        if (!pasajero.Activo)
            throw new FlowRideExcepcion("El pasajero está inactivo y no puede solicitar viajes.");
        if (!empresa.Activa)
            throw new FlowRideExcepcion($"La empresa cliente {empresa.Nombre} está inactiva.");
    }

    public static void ValidarModificable(SolicitudViaje solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (!solicitud.EsModificable)
            throw new FlowRideExcepcion(
                $"La solicitud está {solicitud.Estado}: solo se modifica sin asignar; cancélala y registra otra.");
    }
}

/// <summary>Itinerarios de las empresas cliente (RF-08).</summary>
public static class ItinerarioDominio
{
    public const int MaximoDias = 366;

    /// <summary>
    /// Fechas del rango que caen en los días de la semana pedidos (todos si no se indican). Un itinerario recurrente se
    /// guarda como un itinerario por fecha.
    /// </summary>
    public static IReadOnlyList<DateOnly> Fechas(DateOnly desde, DateOnly hasta, IReadOnlyCollection<DayOfWeek>? dias)
    {
        if (hasta < desde)
            throw new FlowRideExcepcion("La fecha final no puede ser anterior a la inicial.");
        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDias)
            throw new FlowRideExcepcion($"El rango no puede superar {MaximoDias} días.");

        var fechas = Enumerable.Range(0, hasta.DayNumber - desde.DayNumber + 1)
            .Select(desde.AddDays)
            .Where(fecha => dias is null || dias.Count == 0 || dias.Contains(fecha.DayOfWeek))
            .ToList();
        return fechas.Count == 0 ? throw new FlowRideExcepcion("El rango no contiene ninguno de los días pedidos.") : fechas;
    }

    public static void ValidarHorario(TimeOnly salida, TimeOnly llegada)
    {
        if (llegada <= salida)
            throw new FlowRideExcepcion("La hora de llegada debe ser posterior a la de salida.");
    }

    /// <summary>Una solicitud solo puede tomar un itinerario de la empresa de su pasajero.</summary>
    public static void ValidarDeLaEmpresa(Itinerario itinerario, Guid empresaClienteId)
    {
        ArgumentNullException.ThrowIfNull(itinerario);
        if (itinerario.EmpresaClienteId != empresaClienteId)
            throw new FlowRideExcepcion("El itinerario no pertenece a la empresa del pasajero.");
    }
}

/// <summary>Quién lee una solicitud desde el Panel Web o el canal.</summary>
public enum LectorSolicitud
{
    Pasajero,
    OperadorLogistico,
    Administrador
}

/// <summary>
/// Visibilidad de las solicitudes personales (PAS-SEG-01, parámetro <c>solicitud.visibilidad_personales</c>): el
/// pasajero siempre ve las suyas; el Operador logístico, solo si el parámetro lo permite; el Administrador, nunca. Quien
/// no puede verlas ve la solicitud sin direcciones.
/// </summary>
public static class VisibilidadSolicitudes
{
    public static bool PuedeVerDetalle(bool esPersonal, LectorSolicitud lector, string visibilidad) =>
        !esPersonal
        || lector == LectorSolicitud.Pasajero
        || (lector == LectorSolicitud.OperadorLogistico
            && visibilidad == CatalogoParametros.VisiblesParaPasajeroYOperadorLogistico);
}
