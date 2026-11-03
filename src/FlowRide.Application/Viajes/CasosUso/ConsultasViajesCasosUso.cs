using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.Application.Viajes.CasosUso;

/// <summary>Historial de viajes (RF-31), con la visibilidad de las solicitudes personales (PAS-SEG-01).</summary>
public sealed class BuscarViajesCasoUso(IConsultasViajes consultas, ParametrosDominio parametros)
    : ICasoUsoConRetorno<BuscarViajesDominio, PaginaResultado<ViajeLectura>>
{
    public async Task<PaginaResultado<ViajeLectura>> EjecutarAsync(BuscarViajesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var pagina = await consultas.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
        var visibilidad = await parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);
        return pagina.Convertir(viaje => VisibilidadViajes.Aplicar(viaje, datos.Lector, visibilidad));
    }
}

/// <summary>Un viaje con sus paradas, incidentes y calificación.</summary>
public sealed class ConsultarViajeCasoUso(IConsultasViajes consultas, ParametrosDominio parametros)
    : ICasoUsoConRetorno<ConsultarViajeDominio, ViajeDetalleLectura>
{
    public async Task<ViajeDetalleLectura> EjecutarAsync(ConsultarViajeDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var detalle = await consultas.DetalleAsync(datos.ViajeId, ct)
                      ?? throw new FlowRideExcepcion($"El viaje {datos.ViajeId} no existe.");
        var visibilidad = await parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);
        return VisibilidadSolicitudes.PuedeVerDetalle(detalle.Viaje.EsPersonal, datos.Lector, visibilidad) ? detalle : detalle.SinDirecciones();
    }
}

/// <summary>
/// Monitoreo de la operación (RF-37): viajes en curso y los programados que empiezan en las próximas horas. Un viaje
/// programado cuya hora ya pasó aparece como retrasado (sin GPS: ADR-0005 está fuera de alcance).
/// </summary>
public sealed class MonitorearViajesCasoUso(IConsultasViajes consultas, ParametrosDominio parametros, TimeProvider reloj)
    : ICasoUsoConRetorno<MonitorearViajesDominio, IReadOnlyList<MonitoreoViajeLectura>>
{
    /// <summary>Cuánto hacia adelante se muestran los viajes programados.</summary>
    public static readonly TimeSpan HorizonteProgramados = TimeSpan.FromHours(2);

    public async Task<IReadOnlyList<MonitoreoViajeLectura>> EjecutarAsync(MonitorearViajesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var visibilidad = await parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);
        return (await consultas.ActivosAsync(ahora + HorizonteProgramados, ct))
            .Select(viaje =>
            {
                var retraso = viaje.Estado == EstadoViaje.Programado && viaje.InicioProgramado < ahora
                    ? (int)(ahora - viaje.InicioProgramado).TotalMinutes
                    : 0;
                return new MonitoreoViajeLectura(VisibilidadViajes.Aplicar(viaje, datos.Lector, visibilidad), retraso > 0, retraso);
            })
            .ToList();
    }
}

/// <summary>Trazabilidad de un servicio de punta a punta (RF-40): solicitud, motor, asignaciones, viaje y auditoría.</summary>
public sealed class TrazabilidadSolicitudCasoUso(IConsultasViajes consultas)
    : ICasoUsoConRetorno<TrazabilidadSolicitudDominio, TrazabilidadLectura>
{
    public async Task<TrazabilidadLectura> EjecutarAsync(TrazabilidadSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return await consultas.TrazabilidadAsync(datos.SolicitudId, ct)
               ?? throw new FlowRideExcepcion($"La solicitud {datos.SolicitudId} no existe.");
    }
}

internal static class VisibilidadViajes
{
    public static ViajeLectura Aplicar(ViajeLectura viaje, LectorSolicitud lector, string visibilidad) =>
        VisibilidadSolicitudes.PuedeVerDetalle(viaje.EsPersonal, lector, visibilidad) ? viaje : viaje.SinDirecciones();
}
