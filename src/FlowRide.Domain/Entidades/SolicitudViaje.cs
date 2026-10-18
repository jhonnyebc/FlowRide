using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>
/// Solicitud de transporte de un pasajero (RF-09). Cada cambio de estado queda en el historial (PAS-CONF-02).
/// Ciclo: Pendiente → Asignada → EnCurso → Completada; Pendiente → SinCandidatos; y Cancelada antes del viaje.
/// </summary>
public sealed class SolicitudViaje
{
    private readonly List<EstadoSolicitudHistorial> _historial = [];

#pragma warning disable CS8618 // Solo para materializar desde la base: el constructor público registra el historial inicial.
    private SolicitudViaje() { }
#pragma warning restore CS8618

    public SolicitudViaje(
        Guid id,
        Guid pasajeroId,
        string direccionOrigen,
        string direccionDestino,
        DateTimeOffset fechaHoraServicio,
        TimeSpan duracionEstimada,
        int cantidadPasajeros,
        CanalSolicitud canal,
        DateTimeOffset registradaEn,
        bool urgente = false,
        bool esPersonal = false,
        Guid? empresaClienteId = null,
        Guid? itinerarioId = null,
        Guid? registradaPorId = null)
    {
        Id = Guardas.Id(id, "La solicitud requiere un Id.", nameof(id));
        PasajeroId = Guardas.Id(pasajeroId, "La solicitud requiere un pasajero.", nameof(pasajeroId));
        DireccionOrigen = Guardas.Texto(direccionOrigen, nameof(direccionOrigen));
        DireccionDestino = Guardas.Texto(direccionDestino, nameof(direccionDestino));
        if (duracionEstimada <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duracionEstimada), "La duración estimada debe ser positiva.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidadPasajeros);

        FechaHoraServicio = fechaHoraServicio;
        DuracionEstimada = duracionEstimada;
        CantidadPasajeros = cantidadPasajeros;
        Canal = canal;
        RegistradaEn = registradaEn;
        Urgente = urgente;
        EsPersonal = esPersonal;
        EmpresaClienteId = empresaClienteId;
        ItinerarioId = itinerarioId;

        Estado = EstadoSolicitud.Pendiente;
        _historial.Add(new EstadoSolicitudHistorial(
            Guid.CreateVersion7(registradaEn), Id, null, EstadoSolicitud.Pendiente, registradaEn, registradaPorId ?? pasajeroId, null));
    }

    public Guid Id { get; }
    public Guid PasajeroId { get; }
    public Guid? EmpresaClienteId { get; }
    public Guid? ItinerarioId { get; }
    public string DireccionOrigen { get; private set; }
    public string DireccionDestino { get; private set; }
    public DateTimeOffset FechaHoraServicio { get; private set; }
    public TimeSpan DuracionEstimada { get; private set; }
    public int CantidadPasajeros { get; private set; }

    /// <summary>Servicio crítico (RF-13).</summary>
    public bool Urgente { get; private set; }

    /// <summary>Solicitud personal: visibilidad restringida (PAS-SEG-01).</summary>
    public bool EsPersonal { get; }

    public CanalSolicitud Canal { get; }
    public DateTimeOffset RegistradaEn { get; }
    public EstadoSolicitud Estado { get; private set; }
    public CancelacionSolicitud? Cancelacion { get; private set; }
    public IReadOnlyList<EstadoSolicitudHistorial> Historial => _historial;

    public DateOnly FechaServicio => DateOnly.FromDateTime(FechaHoraServicio.DateTime);

    public FranjaHoraria Franja() => new(FechaHoraServicio, FechaHoraServicio + DuracionEstimada);


    /// <summary>Puede modificarse mientras no esté asignada (RF-32): en estado Pendiente o SinCandidatos.</summary>
    public bool EsModificable => Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.SinCandidatos;

    /// <summary>Solicitud que todavía ocupa una franja del pasajero (no terminada ni cancelada).</summary>
    public bool EstaActiva => Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Asignada
        or EstadoSolicitud.SinCandidatos or EstadoSolicitud.EnCurso;

    /// <summary>
    /// Ajusta los datos del servicio (RF-32). Solo sin asignar; una solicitud sin candidatos vuelve a la cola de
    /// asignación para que el motor la intente de nuevo con los datos nuevos.
    /// </summary>
    public void Modificar(
        string direccionOrigen,
        string direccionDestino,
        DateTimeOffset fechaHoraServicio,
        TimeSpan duracionEstimada,
        int cantidadPasajeros,
        bool urgente,
        Guid? actorId,
        DateTimeOffset ahora)
    {
        if (!EsModificable)
            throw Guardas.TransicionInvalida("modificar", "la solicitud", Estado);
        if (duracionEstimada <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duracionEstimada), "La duración estimada debe ser positiva.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidadPasajeros);

        DireccionOrigen = Guardas.Texto(direccionOrigen, nameof(direccionOrigen));
        DireccionDestino = Guardas.Texto(direccionDestino, nameof(direccionDestino));
        FechaHoraServicio = fechaHoraServicio;
        DuracionEstimada = duracionEstimada;
        CantidadPasajeros = cantidadPasajeros;
        Urgente = urgente;

        if (Estado == EstadoSolicitud.SinCandidatos)
            CambiarEstado(EstadoSolicitud.Pendiente, actorId, "Solicitud modificada.", ahora);
    }
    public void MarcarAsignada(Asignacion asignacion, Guid? actorId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(asignacion);
        if (asignacion.SolicitudId != Id)
            throw new InvalidOperationException("La asignación no corresponde a esta solicitud.");
        Exigir(EstadoSolicitud.Pendiente, "asignar");
        CambiarEstado(EstadoSolicitud.Asignada, actorId, null, ahora);
    }

    /// <summary>El motor no encontró candidatos: se alerta al Operador logístico (RF-13).</summary>
    public void MarcarSinCandidatos(string motivo, DateTimeOffset ahora)
    {
        Exigir(EstadoSolicitud.Pendiente, "marcar sin candidatos");
        CambiarEstado(EstadoSolicitud.SinCandidatos, null, Guardas.Texto(motivo, nameof(motivo)), ahora);
    }

    /// <summary>Vuelve a la cola de asignación: asignación rechazada o nuevo intento tras quedar sin candidatos.</summary>
    public void VolverAPendiente(string motivo, Guid? actorId, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoSolicitud.Asignada or EstadoSolicitud.SinCandidatos))
            throw Guardas.TransicionInvalida("devolver a pendiente", "la solicitud", Estado);
        CambiarEstado(EstadoSolicitud.Pendiente, actorId, Guardas.Texto(motivo, nameof(motivo)), ahora);
    }

    public void MarcarEnCurso(DateTimeOffset ahora)
    {
        Exigir(EstadoSolicitud.Asignada, "iniciar");
        CambiarEstado(EstadoSolicitud.EnCurso, null, null, ahora);
    }

    public void MarcarCompletada(DateTimeOffset ahora)
    {
        Exigir(EstadoSolicitud.EnCurso, "completar");
        CambiarEstado(EstadoSolicitud.Completada, null, null, ahora);
    }

    /// <summary>Cancela la solicitud antes de que el viaje empiece (RF-32, PAS-AUD-02).</summary>
    public CancelacionSolicitud Cancelar(Guid cancelacionId, string motivo, Guid actorId, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoSolicitud.Pendiente or EstadoSolicitud.Asignada or EstadoSolicitud.SinCandidatos))
            throw Guardas.TransicionInvalida("cancelar", "la solicitud", Estado);

        Cancelacion = new CancelacionSolicitud(cancelacionId, Id, motivo, ahora, actorId);
        CambiarEstado(EstadoSolicitud.Cancelada, actorId, Cancelacion.Motivo, ahora);
        return Cancelacion;
    }

    private void Exigir(EstadoSolicitud esperado, string accion)
    {
        if (Estado != esperado)
            throw Guardas.TransicionInvalida(accion, "la solicitud", Estado);
    }

    private void CambiarEstado(EstadoSolicitud nuevo, Guid? actorId, string? motivo, DateTimeOffset ahora)
    {
        _historial.Add(new EstadoSolicitudHistorial(Guid.CreateVersion7(ahora), Id, Estado, nuevo, ahora, actorId, motivo));
        Estado = nuevo;
    }
}
