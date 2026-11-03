using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Horarios.Entrada.Puertos;
using FlowRide.Domain.Horarios;

namespace FlowRide.Application.Horarios.Entrada.Adaptadores;

public sealed class HorariosAdaptadorEntrada(
    ICasoUsoConRetorno<RecalcularHorariosDominio, ResultadoRecalculoHorarios> recalcular,
    TimeProvider reloj) : IHorariosPuertoEntrada
{
    /// <summary>Margen hacia atrás: un servicio de hace unas horas puede seguir en curso.</summary>
    public static readonly TimeSpan Margen = TimeSpan.FromDays(1);

    public async Task<int> RecalcularAsync(CancellationToken ct) =>
        (await recalcular.EjecutarAsync(new RecalcularHorariosDominio(reloj.GetUtcNow() - Margen), ct)).Total;
}
