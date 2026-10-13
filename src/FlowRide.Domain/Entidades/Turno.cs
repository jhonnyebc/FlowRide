using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

public sealed class Turno
{
    public Turno(
        Guid id,
        Guid conductorId,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        EstadoTurno estado = EstadoTurno.Programado)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El turno requiere un Id.", nameof(id));
        if (conductorId == Guid.Empty)
            throw new ArgumentException("El turno requiere un conductor.", nameof(conductorId));
        if (fin <= inicio)
            throw new ArgumentException("El fin del turno debe ser posterior a su inicio.", nameof(fin));

        Id = id;
        ConductorId = conductorId;
        Inicio = inicio;
        Fin = fin;
        Estado = estado;
    }

    public Guid Id { get; }
    public Guid ConductorId { get; }
    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fin { get; }
    public EstadoTurno Estado { get; }

    public bool EstaVigente => Estado is EstadoTurno.Programado or EstadoTurno.Activo;

    /// <summary>Intervalo semiabierto [Inicio, Fin) de un turno vigente.</summary>
    public bool Cubre(DateTimeOffset instante) => EstaVigente && Inicio <= instante && instante < Fin;
}
