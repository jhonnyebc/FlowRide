using FlowRide.Application.Asignaciones.Entrada.Puertos;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Viajes.Entrada.Puertos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowRide.Infrastructure.Asignaciones;

/// <summary>Opciones del worker de la cola de asignación (decisión del 2026-10-18: cada 15 s).</summary>
public sealed class WorkerAsignacionOptions
{
    public const string Seccion = "FlowRide:Asignacion:Worker";

    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 15;
    public int LoteMaximo { get; set; } = 20;
}

/// <summary>
/// Adaptador de entrada en segundo plano (RF-10): cada intervalo primero vence las asignaciones que el conductor no
/// respondió en el plazo (Fase 6: vuelven a la cola sin ese conductor) y luego toma la cola en el orden en que se atiende
/// (urgentes primero), cada solicitud en su propio alcance y transacción. Si otra réplica ya la tomó, el conflicto se
/// registra y la solicitud se reintenta en la siguiente vuelta; un error en una no detiene las demás.
/// </summary>
public sealed partial class WorkerColaAsignacion(
    IServiceScopeFactory alcances,
    WorkerAsignacionOptions opciones,
    TimeProvider reloj,
    ILogger<WorkerColaAsignacion> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.IntervaloSegundos)), reloj);
        do
        {
            try
            {
                await ProcesarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                VueltaFallida(logger, error);
            }
        } while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Una vuelta de la cola. Devuelve cuántas solicitudes procesó (asignadas o sin candidatos).</summary>
    public async Task<int> ProcesarAsync(CancellationToken ct)
    {
        await VencerSinRespuestaAsync(ct);

        IReadOnlyList<Guid> pendientes;
        await using (var alcance = alcances.CreateAsyncScope())
            pendientes = await alcance.ServiceProvider.GetRequiredService<IAsignacionesPuertoEntrada>().PendientesAsync(opciones.LoteMaximo, ct);

        var procesadas = 0;
        foreach (var solicitudId in pendientes)
        {
            await using var alcance = alcances.CreateAsyncScope();
            try
            {
                var resultado = await alcance.ServiceProvider.GetRequiredService<IAsignacionesPuertoEntrada>().AsignarAsync(solicitudId, ct);
                SolicitudProcesada(logger, solicitudId, resultado.Resultado);
                procesadas++;
            }
            catch (ConflictoConcurrenciaExcepcion)
            {
                SolicitudEnConflicto(logger, solicitudId);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                SolicitudFallida(logger, solicitudId, error);
            }
        }
        return procesadas;
    }

    /// <summary>Vence las asignaciones sin respuesta. Devuelve cuántas venció.</summary>
    public async Task<int> VencerSinRespuestaAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> sinRespuesta;
        await using (var alcance = alcances.CreateAsyncScope())
            sinRespuesta = await alcance.ServiceProvider.GetRequiredService<IViajesPuertoEntrada>().SinRespuestaAsync(opciones.LoteMaximo, ct);

        var vencidas = 0;
        foreach (var asignacionId in sinRespuesta)
        {
            await using var alcance = alcances.CreateAsyncScope();
            try
            {
                if (!await alcance.ServiceProvider.GetRequiredService<IViajesPuertoEntrada>().VencerAsignacionAsync(asignacionId, ct))
                    continue;
                AsignacionVencida(logger, asignacionId);
                vencidas++;
            }
            catch (ConflictoConcurrenciaExcepcion)
            {
                AsignacionEnConflicto(logger, asignacionId);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                VencimientoFallido(logger, asignacionId, error);
            }
        }
        return vencidas;
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Cola de asignación: solicitud {SolicitudId} → {Resultado}.")]
    private static partial void SolicitudProcesada(ILogger logger, Guid solicitudId, string resultado);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information,
        Message = "Cola de asignación: la solicitud {SolicitudId} cambió mientras se asignaba; se reintentará.")]
    private static partial void SolicitudEnConflicto(ILogger logger, Guid solicitudId);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Cola de asignación: no se pudo asignar la solicitud {SolicitudId}.")]
    private static partial void SolicitudFallida(ILogger logger, Guid solicitudId, Exception error);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Error, Message = "Cola de asignación: la vuelta falló.")]
    private static partial void VueltaFallida(ILogger logger, Exception error);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Information,
        Message = "Cola de asignación: la asignación {AsignacionId} venció sin respuesta del conductor; la solicitud vuelve a la cola.")]
    private static partial void AsignacionVencida(ILogger logger, Guid asignacionId);

    [LoggerMessage(EventId = 5006, Level = LogLevel.Information,
        Message = "Cola de asignación: la asignación {AsignacionId} cambió mientras vencía; se revisará en la siguiente vuelta.")]
    private static partial void AsignacionEnConflicto(ILogger logger, Guid asignacionId);

    [LoggerMessage(EventId = 5007, Level = LogLevel.Error, Message = "Cola de asignación: no se pudo vencer la asignación {AsignacionId}.")]
    private static partial void VencimientoFallido(ILogger logger, Guid asignacionId, Exception error);
}
