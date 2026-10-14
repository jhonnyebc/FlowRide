using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>
/// Conductor y vehículo asignados a una solicitud, automática (motor, RF-10) o manualmente (RF-12).
/// Una solicitud tiene a lo sumo una asignación vigente; la base lo garantiza con una restricción única parcial (ADR-0007).
/// </summary>
public sealed class Asignacion
{
    public Asignacion(
        Guid id,
        Guid solicitudId,
        Guid conductorId,
        Guid vehiculoId,
        FranjaHoraria franja,
        OrigenAsignacion origen,
        DateTimeOffset asignadaEn,
        Guid? decisionId = null,
        Guid? asignacionReemplazadaId = null)
    {
        Id = Guardas.Id(id, "La asignación requiere un Id.", nameof(id));
        SolicitudId = Guardas.Id(solicitudId, "La asignación requiere una solicitud.", nameof(solicitudId));
        ConductorId = Guardas.Id(conductorId, "La asignación requiere un conductor.", nameof(conductorId));
        VehiculoId = Guardas.Id(vehiculoId, "La asignación requiere un vehículo.", nameof(vehiculoId));
        ArgumentNullException.ThrowIfNull(franja);
        if (origen == OrigenAsignacion.Automatica && decisionId is null)
            throw new ArgumentException("Una asignación automática debe referenciar la decisión del motor.", nameof(decisionId));

        Franja = franja;
        Origen = origen;
        AsignadaEn = asignadaEn;
        DecisionId = decisionId;
        AsignacionReemplazadaId = asignacionReemplazadaId;
        Estado = EstadoAsignacion.Asignada;
    }

    public Guid Id { get; }
    public Guid SolicitudId { get; }
    public Guid ConductorId { get; }
    public Guid VehiculoId { get; }
    public FranjaHoraria Franja { get; }
    public OrigenAsignacion Origen { get; }
    public DateTimeOffset AsignadaEn { get; }

    /// <summary>Referencia por identidad al decision log del motor (ADR-0009); null en asignaciones manuales.</summary>
    public Guid? DecisionId { get; }

    /// <summary>Asignación anterior a la que esta reemplaza (RF-12).</summary>
    public Guid? AsignacionReemplazadaId { get; }

    public EstadoAsignacion Estado { get; private set; }

    /// <summary>Motivo del rechazo, la cancelación o el reemplazo.</summary>
    public string? MotivoCierre { get; private set; }

    public bool EstaVigente => Estado is EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada or EstadoAsignacion.EnCurso;

    public void Aceptar()
    {
        Exigir(EstadoAsignacion.Asignada, "aceptar");
        Estado = EstadoAsignacion.Aceptada;
    }

    public void Rechazar(string motivo)
    {
        Exigir(EstadoAsignacion.Asignada, "rechazar");
        Cerrar(EstadoAsignacion.Rechazada, motivo);
    }

    /// <summary>El conductor inicia el servicio: la asignación pasa a EnCurso y nace el viaje.</summary>
    public Viaje Iniciar(Guid viajeId, DateTimeOffset etaProgramada, DateTimeOffset ahora)
    {
        Exigir(EstadoAsignacion.Aceptada, "iniciar");
        var viaje = new Viaje(viajeId, Id, etaProgramada);
        viaje.Iniciar(ahora);
        Estado = EstadoAsignacion.EnCurso;
        return viaje;
    }

    public void Finalizar()
    {
        Exigir(EstadoAsignacion.EnCurso, "finalizar");
        Estado = EstadoAsignacion.Finalizada;
    }

    public void Cancelar(string motivo)
    {
        if (Estado is not (EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada))
            throw Guardas.TransicionInvalida("cancelar", "la asignación", Estado);
        Cerrar(EstadoAsignacion.Cancelada, motivo);
    }

    /// <summary>
    /// Reasignación manual del Operador logístico (RF-12, RF-39): esta asignación queda Reemplazada y
    /// se crea una nueva, manual, para la misma solicitud y franja.
    /// </summary>
    public Asignacion Reemplazar(Guid nuevaAsignacionId, Guid conductorId, Guid vehiculoId, string motivo, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada))
            throw Guardas.TransicionInvalida("reemplazar", "la asignación", Estado);
        if (conductorId == ConductorId && vehiculoId == VehiculoId)
            throw new InvalidOperationException("El reemplazo debe cambiar el conductor o el vehículo.");

        Cerrar(EstadoAsignacion.Reemplazada, motivo);
        return new Asignacion(nuevaAsignacionId, SolicitudId, conductorId, vehiculoId, Franja,
            OrigenAsignacion.Manual, ahora, decisionId: null, asignacionReemplazadaId: Id);
    }

    private void Exigir(EstadoAsignacion esperado, string accion)
    {
        if (Estado != esperado)
            throw Guardas.TransicionInvalida(accion, "la asignación", Estado);
    }

    private void Cerrar(EstadoAsignacion estado, string motivo)
    {
        MotivoCierre = Guardas.Texto(motivo, nameof(motivo));
        Estado = estado;
    }
}
