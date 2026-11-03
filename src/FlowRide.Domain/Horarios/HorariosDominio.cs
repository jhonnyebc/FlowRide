namespace FlowRide.Domain.Horarios;

/// <summary>
/// Recalcular los instantes UTC de los eventos futuros (servicios, turnos, paradas estimadas) con las reglas vigentes de
/// sus zonas. Se miran desde <see cref="Desde"/> en adelante: lo que ya pasó no se toca.
/// </summary>
public sealed record RecalcularHorariosDominio(DateTimeOffset Desde);

/// <summary>Cuántos eventos futuros cambiaron de instante al recalcularlos.</summary>
public sealed record ResultadoRecalculoHorarios(int Solicitudes, int Asignaciones, int Turnos, int Paradas)
{
    public int Total => Solicitudes + Asignaciones + Turnos + Paradas;
}
