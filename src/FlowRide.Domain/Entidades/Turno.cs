using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>
/// Turno programado de un conductor. Ciclo: Programado → Activo → Finalizado, o Abandonado. Es un evento futuro en un lugar:
/// se guarda en hora local con el nombre de la zona; la franja UTC se deriva de ellas.
/// </summary>
public sealed class Turno
{
#pragma warning disable CS8618 // Solo para materializar desde la base: la franja se carga desde sus columnas.
    private Turno() { }
#pragma warning restore CS8618

    public Turno(
        Guid id,
        Guid conductorId,
        FechaHoraLocal inicio,
        FechaHoraLocal fin,
        EstadoTurno estado = EstadoTurno.Programado)
    {
        Id = Guardas.Id(id, "El turno requiere un Id.", nameof(id));
        ConductorId = Guardas.Id(conductorId, "El turno requiere un conductor.", nameof(conductorId));
        ArgumentNullException.ThrowIfNull(inicio);
        ArgumentNullException.ThrowIfNull(fin);
        if (inicio.Zona != fin.Zona)
            throw new ArgumentException("El inicio y el fin del turno van en la misma zona horaria.", nameof(fin));
        InicioLocal = inicio.Hora;
        FinLocal = fin.Hora;
        ZonaHoraria = inicio.Zona;
        Franja = new FranjaHoraria(inicio.Instante(), fin.Instante());
        Estado = estado;
    }

    public Guid Id { get; }
    public Guid ConductorId { get; }
    public DateTime InicioLocal { get; }
    public DateTime FinLocal { get; }
    public string ZonaHoraria { get; } = string.Empty;

    /// <summary>Franja UTC derivada de la hora local y la zona; se pone al día con <see cref="RecalcularInstantes"/>.</summary>
    public FranjaHoraria Franja { get; private set; }
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

    /// <summary>Recalcula la franja UTC con las reglas vigentes de la zona; true si cambió.</summary>
    public bool RecalcularInstantes()
    {
        var franja = new FranjaHoraria(new FechaHoraLocal(InicioLocal, ZonaHoraria).Instante(), new FechaHoraLocal(FinLocal, ZonaHoraria).Instante());
        if (franja == Franja)
            return false;
        Franja = franja;
        return true;
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
