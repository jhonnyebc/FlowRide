using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

/// <summary>Empleado del cliente empresarial; interactúa solo por WhatsApp.</summary>
public sealed class Pasajero : Usuario
{
    public Pasajero(
        Guid id,
        string nombre,
        string telefonoWhatsApp,
        Guid rolId,
        DateTimeOffset creadoEn,
        string codigoInterno,
        Guid empresaClienteId,
        string? correo = null)
        : base(id, nombre, telefonoWhatsApp, rolId, creadoEn, correo)
    {
        CodigoInterno = Guardas.Texto(codigoInterno, nameof(codigoInterno));
        EmpresaClienteId = Guardas.Id(empresaClienteId, "El pasajero requiere una empresa cliente.", nameof(empresaClienteId));
    }

    public string CodigoInterno { get; }
    public Guid EmpresaClienteId { get; }

    /// <summary>Registra una solicitud en estado Pendiente (RF-09).</summary>
    public SolicitudViaje SolicitarViaje(
        Guid solicitudId,
        string direccionOrigen,
        string direccionDestino,
        DateTimeOffset fechaHoraServicio,
        TimeSpan duracionEstimada,
        int cantidadPasajeros,
        CanalSolicitud canal,
        DateTimeOffset ahora,
        bool urgente = false,
        bool esPersonal = false,
        Guid? itinerarioId = null)
    {
        if (!Activo)
            throw new InvalidOperationException("Un pasajero inactivo no puede solicitar viajes.");

        return new SolicitudViaje(
            solicitudId, Id, direccionOrigen, direccionDestino, fechaHoraServicio, duracionEstimada,
            cantidadPasajeros, canal, ahora, urgente, esPersonal, EmpresaClienteId, itinerarioId);
    }

    /// <summary>
    /// Califica al conductor de un viaje finalizado (RF-54). Que el pasajero haya sido parte del viaje
    /// lo valida el caso de uso, que conoce la solicitud asociada.
    /// </summary>
    public Calificacion Calificar(Guid calificacionId, Viaje viaje, int puntaje, string? comentario, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(viaje);
        if (viaje.Estado != EstadoViaje.Finalizado)
            throw new InvalidOperationException("Solo se puede calificar un viaje finalizado.");

        return new Calificacion(calificacionId, viaje.Id, Id, puntaje, comentario, ahora);
    }
}
