using FlowRide.Application.Canal.Entrada.Puertos;
using FlowRide.Application.Comun.Salida.Puertos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowRide.Infrastructure.Canal;

/// <summary>Opciones del worker del canal WhatsApp.</summary>
public sealed class WorkerCanalOptions
{
    public const string Seccion = "FlowRide:WhatsApp:Worker";

    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 2;
    public int LoteMaximo { get; set; } = 20;
}

/// <summary>
/// Adaptador de entrada en segundo plano del canal (decisión del 2026-10-18): cada vuelta procesa los mensajes entrantes
/// en el orden en que llegaron y luego envía la bandeja de salida. Cada mensaje va en su propio alcance y transacción; un
/// conflicto con otra réplica se deja para la siguiente vuelta y un error técnico cuenta un intento.
/// </summary>
public sealed partial class WorkerCanalWhatsApp(
    IServiceScopeFactory alcances,
    WorkerCanalOptions opciones,
    TimeProvider reloj,
    ILogger<WorkerCanalWhatsApp> logger) : BackgroundService
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

    /// <summary>Una vuelta: entrantes y luego salientes. Devuelve cuántos de cada uno se atendieron.</summary>
    public async Task<(int Procesados, int Enviados)> ProcesarAsync(CancellationToken ct)
    {
        var procesados = 0;
        foreach (var mensajeId in await PendientesAsync((puerto, token) => puerto.EntrantesPendientesAsync(opciones.LoteMaximo, token), ct))
        {
            await using var alcance = alcances.CreateAsyncScope();
            var puerto = alcance.ServiceProvider.GetRequiredService<ICanalWhatsAppPuertoEntrada>();
            try
            {
                if (await puerto.ProcesarEntranteAsync(mensajeId, ct))
                    procesados++;
            }
            catch (ConflictoConcurrenciaExcepcion)
            {
                MensajeEnConflicto(logger, mensajeId);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                MensajeFallido(logger, mensajeId, error);
                await RegistrarFalloAsync(mensajeId, error, ct);
            }
        }

        var enviados = 0;
        foreach (var mensajeId in await PendientesAsync((puerto, token) => puerto.SalientesPendientesAsync(opciones.LoteMaximo, token), ct))
        {
            await using var alcance = alcances.CreateAsyncScope();
            try
            {
                var resultado = await alcance.ServiceProvider.GetRequiredService<ICanalWhatsAppPuertoEntrada>().EnviarSalienteAsync(mensajeId, ct);
                if (resultado is null)
                    continue;
                MensajeEnviado(logger, mensajeId, resultado);
                enviados++;
            }
            catch (ConflictoConcurrenciaExcepcion)
            {
                MensajeEnConflicto(logger, mensajeId);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                EnvioFallido(logger, mensajeId, error);
            }
        }
        return (procesados, enviados);
    }

    private async Task<IReadOnlyList<Guid>> PendientesAsync(Func<ICanalWhatsAppPuertoEntrada, CancellationToken, Task<IReadOnlyList<Guid>>> consulta,
        CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        return await consulta(alcance.ServiceProvider.GetRequiredService<ICanalWhatsAppPuertoEntrada>(), ct);
    }

    private async Task RegistrarFalloAsync(Guid mensajeId, Exception error, CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        try
        {
            if (await alcance.ServiceProvider.GetRequiredService<ICanalWhatsAppPuertoEntrada>()
                    .RegistrarFalloEntranteAsync(mensajeId, $"{error.GetType().Name}: {error.Message}", ct))
                MensajeDescartado(logger, mensajeId);
        }
        catch (Exception otro) when (otro is not OperationCanceledException)
        {
            MensajeFallido(logger, mensajeId, otro);
        }
    }

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Canal WhatsApp: mensaje saliente {MensajeId} → {Resultado}.")]
    private static partial void MensajeEnviado(ILogger logger, Guid mensajeId, string resultado);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information,
        Message = "Canal WhatsApp: el mensaje {MensajeId} cambió en otra réplica; se revisará en la siguiente vuelta.")]
    private static partial void MensajeEnConflicto(ILogger logger, Guid mensajeId);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Error, Message = "Canal WhatsApp: no se pudo procesar el mensaje entrante {MensajeId}.")]
    private static partial void MensajeFallido(ILogger logger, Guid mensajeId, Exception error);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Warning,
        Message = "Canal WhatsApp: el mensaje entrante {MensajeId} agotó sus intentos y quedó Fallido.")]
    private static partial void MensajeDescartado(ILogger logger, Guid mensajeId);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Error, Message = "Canal WhatsApp: no se pudo enviar el mensaje {MensajeId}.")]
    private static partial void EnvioFallido(ILogger logger, Guid mensajeId, Exception error);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Error, Message = "Canal WhatsApp: la vuelta falló.")]
    private static partial void VueltaFallida(ILogger logger, Exception error);
}
