using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Horarios.Salida.Puertos;
using FlowRide.Domain.Horarios;

namespace FlowRide.Application.Horarios.CasosUso;

/// <summary>
/// Los eventos futuros se guardan en hora local con el nombre de su zona; su instante UTC es derivado. Cuando las reglas
/// de una zona cambian (nueva base de zonas horarias del sistema o de .NET), este caso de uso vuelve a calcularlos para
/// que cada evento siga cayendo a la hora local acordada. Las asignaciones vigentes siguen a su servicio.
/// </summary>
public sealed class RecalcularHorariosCasoUso(IHorariosFuturos horarios, IUnidadTrabajo unidadTrabajo)
    : ICasoUsoConRetorno<RecalcularHorariosDominio, ResultadoRecalculoHorarios>
{
    public async Task<ResultadoRecalculoHorarios> EjecutarAsync(RecalcularHorariosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        int solicitudes = 0, asignaciones = 0;
        foreach (var (solicitud, vigentes) in await horarios.SolicitudesAsync(datos.Desde, ct))
        {
            if (!solicitud.RecalcularInstante())
                continue;
            solicitudes++;
            asignaciones += vigentes.Count(asignacion => asignacion.SeguirServicio(solicitud.Franja()));
        }

        var turnos = (await horarios.TurnosAsync(datos.Desde, ct)).Count(turno => turno.RecalcularInstantes());
        var paradas = (await horarios.ParadasAsync(datos.Desde, ct)).Count(parada => parada.RecalcularInstante());

        var resultado = new ResultadoRecalculoHorarios(solicitudes, asignaciones, turnos, paradas);
        if (resultado.Total > 0)
            await unidadTrabajo.GuardarCambiosAsync(ct);
        return resultado;
    }
}
