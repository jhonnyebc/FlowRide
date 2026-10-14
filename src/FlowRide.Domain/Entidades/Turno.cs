using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>Turno programado de un conductor. Ciclo: Programado → Activo → Finalizado, o Abandonado.</summary>
public sealed class Turno
{
    public Turno(
        Guid id,
        Guid conductorId,
        DateTimeOffset inicio,
        DateTimeOffset fin,
        EstadoTurno estado = EstadoTurno.Programado)
    {
        Id = Guardas.Id(id, "El turno requiere un Id.", nameof(id));
        ConductorId = Guardas.Id(conductorId, "El turno requiere un conductor.", nameof(conductorId));
        Franja = new FranjaHoraria(inicio, fin);
        Estado = estado;
    }

    public Guid Id { get; }
    public Guid ConductorId { get; }
    public FranjaHoraria Franja { get; }
    public DateTimeOffset Inicio => Franja.Inicio;
    public DateTimeOffset Fin => Franja.Fin;
    public EstadoTurno Estado { get; private set; }
    public string? MotivoAbandono { get; private set; }

    public bool EstaVigente => Estado is EstadoTurno.Programado or EstadoTurno.Activo;

    /// <summary>Un turno vigente cubre un instante del intervalo semiabierto [Inicio, Fin).</summary>
    public bool Cubre(DateTimeOffset instante) => EstaVigente && Franja.Contiene(instante);

    /// <summary>Un turno vigente cubre una franja de servicio que cabe completa dentro de él.</summary>
    public bool Cubre(FranjaHoraria franja)
    {
        ArgumentNullException.ThrowIfNull(franja);
        return EstaVigente && franja.EstaContenidaEn(Franja);
    }

    public void Iniciar()
    {
        if (Estado != EstadoTurno.Programado)
            throw Guardas.TransicionInvalida("iniciar", "el turno", Estado);
        Estado = EstadoTurno.Activo;
    }

    public void Finalizar()
    {
        if (Estado != EstadoTurno.Activo)
            throw Guardas.TransicionInvalida("finalizar", "el turno", Estado);
        Estado = EstadoTurno.Finalizado;
    }

    /// <summary>Contingencia del caso ancla: el conductor abandona el turno (RF-12).</summary>
    public void MarcarAbandonado(string motivo)
    {
        if (!EstaVigente)
            throw Guardas.TransicionInvalida("abandonar", "el turno", Estado);
        MotivoAbandono = Guardas.Texto(motivo, nameof(motivo));
        Estado = EstadoTurno.Abandonado;
    }
}
