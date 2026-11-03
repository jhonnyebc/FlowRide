using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.Viajes.Repositorios;

namespace FlowRide.Domain.Viajes;

/// <summary>Reglas de los viajes y su calificación que consultan el Silo o los parámetros del operador (RF-34, RF-54).</summary>
public sealed class ViajeDominio(IViajeRepositorio viajes, ICalificacionRepositorio calificaciones, ParametrosDominio parametros)
{
    public async Task<Viaje> ValidarExistaAsync(Guid viajeId, CancellationToken ct) =>
        await viajes.ConsultarPorIdAsync(viajeId, ct)
        ?? throw new FlowRideExcepcion($"El viaje {viajeId} no existe.");

    /// <summary>Plazo para aceptar o rechazar (parámetro <c>asignacion.plazo_respuesta_minutos</c>).</summary>
    public async Task<TimeSpan> PlazoRespuestaAsync(CancellationToken ct) =>
        TimeSpan.FromMinutes(await parametros.EnteroAsync(CatalogoParametros.PlazoRespuestaAsignacionMinutos, ct));

    /// <summary>
    /// Calificación (RF-54, PAS-AUD-03): el viaje terminó, quien califica es el pasajero de la solicitud, todavía está
    /// dentro del plazo (parámetro <c>calificacion.plazo_dias</c>) y no lo había calificado.
    /// </summary>
    public async Task ValidarCalificacionAsync(Viaje viaje, SolicitudViaje solicitud, DateTimeOffset ahora, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(viaje);
        ArgumentNullException.ThrowIfNull(solicitud);
        if (viaje.Estado != EstadoViaje.Finalizado || viaje.FechaHoraFin is not { } fin)
            throw new FlowRideExcepcion($"El viaje está {viaje.Estado}: solo se califica un viaje finalizado.");

        var plazo = await parametros.EnteroAsync(CatalogoParametros.PlazoCalificacionDias, ct);
        if (ahora > fin.AddDays(plazo))
            throw new FlowRideExcepcion($"El plazo para calificar el viaje venció ({plazo} días después de terminado).");
        if (await calificaciones.ExisteAsync(viaje.Id, solicitud.PasajeroId, ct))
            throw new FlowRideExcepcion("El pasajero ya calificó este viaje.");
    }
}

public static class ViajeRepositorioExtensiones
{
    /// <summary>
    /// La asignación se cancela o se reemplaza antes de empezar: si ya había sido aceptada, su viaje programado se cancela
    /// también. Devuelve el viaje cancelado, si había.
    /// </summary>
    public static async Task<Viaje?> CancelarProgramadoDeAsync(this IViajeRepositorio viajes, Guid asignacionId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(viajes);
        if (await viajes.ConsultarPorAsignacionAsync(asignacionId, ct) is not { Estado: EstadoViaje.Programado } viaje)
            return null;

        viaje.Cancelar();
        return viaje;
    }
}

/// <summary>Reglas puras del ciclo del viaje.</summary>
public static class ReglasViaje
{
    /// <summary>¿La asignación sigue sin respuesta después del plazo? Solo vencen las que esperan respuesta (Asignada).</summary>
    public static bool RespuestaVencida(Asignacion asignacion, TimeSpan plazo, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(asignacion);
        return asignacion.Estado == EstadoAsignacion.Asignada && asignacion.AsignadaEn + plazo <= ahora;
    }

    public static string MotivoVencimiento(TimeSpan plazo) =>
        $"El conductor no respondió en {(int)plazo.TotalMinutes} minutos.";

    /// <summary>
    /// Ejecuta una transición de las entidades: si el estado no la permite (o los datos no cumplen), la explica como regla
    /// de negocio (400) en vez de un error interno.
    /// </summary>
    public static T Aplicar<T>(Func<T> transicion)
    {
        ArgumentNullException.ThrowIfNull(transicion);
        try
        {
            return transicion();
        }
        catch (InvalidOperationException error)
        {
            throw new FlowRideExcepcion(error.Message, error);
        }
        catch (ArgumentException error)
        {
            var mensaje = error.ParamName is null ? error.Message : error.Message.Replace($" (Parameter '{error.ParamName}')", string.Empty);
            throw new FlowRideExcepcion(mensaje, error);
        }
    }

    public static void Aplicar(Action transicion)
    {
        ArgumentNullException.ThrowIfNull(transicion);
        Aplicar(() =>
        {
            transicion();
            return true;
        });
    }
}
