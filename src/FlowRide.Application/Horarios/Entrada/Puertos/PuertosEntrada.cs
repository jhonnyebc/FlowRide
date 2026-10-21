namespace FlowRide.Application.Horarios.Entrada.Puertos;

public interface IHorariosPuertoEntrada
{
    /// <summary>Recalcula los instantes de los eventos futuros con las reglas vigentes; devuelve cuántos cambiaron.</summary>
    Task<int> RecalcularAsync(CancellationToken ct);
}
