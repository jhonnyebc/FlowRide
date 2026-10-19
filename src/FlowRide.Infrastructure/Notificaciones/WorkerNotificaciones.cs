using FlowRide.Application.Notificaciones.Entrada.Puertos;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Persistencia.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowRide.Infrastructure.Notificaciones;

/// <summary>Opciones del worker de notificaciones.</summary>
public sealed class WorkerNotificacionesOptions
{
    public const string Seccion = "FlowRide:Notificaciones:Worker";

    public bool Habilitado { get; set; } = true;
    public int IntervaloSegundos { get; set; } = 2;
    public int LoteMaximo { get; set; } = 50;
}

/// <summary>
/// Procesador de la Outbox (ADR-0007, ADR-0015): toma cada evento listo con <c>FOR UPDATE SKIP LOCKED</c> (varias réplicas
/// no se pisan), lo convierte en notificaciones y lo marca procesado en la misma transacción; un error lo devuelve a la
/// cola con espera creciente. Después envía los correos pendientes. WhatsApp lo envía <c>WorkerCanalWhatsApp</c>.
/// </summary>
public sealed partial class WorkerNotificaciones(
    IServiceScopeFactory alcances,
    WorkerNotificacionesOptions opciones,
    TimeProvider reloj,
    ILogger<WorkerNotificaciones> logger) : BackgroundService
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

    /// <summary>Una vuelta: eventos de la Outbox y luego correos. Devuelve cuántos de cada uno se atendieron.</summary>
    public async Task<(int Eventos, int Correos)> ProcesarAsync(CancellationToken ct)
    {
        var eventos = 0;
        while (eventos < opciones.LoteMaximo && await ProcesarSiguienteEventoAsync(ct))
            eventos++;

        IReadOnlyList<Guid> pendientes;
        await using (var alcance = alcances.CreateAsyncScope())
            pendientes = await alcance.ServiceProvider.GetRequiredService<INotificacionesPuertoEntrada>().CorreosPendientesAsync(opciones.LoteMaximo, ct);

        var correos = 0;
        foreach (var correoId in pendientes)
        {
            await using var alcance = alcances.CreateAsyncScope();
            try
            {
                if (await alcance.ServiceProvider.GetRequiredService<INotificacionesPuertoEntrada>().EnviarCorreoAsync(correoId, ct))
                    correos++;
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                CorreoFallido(logger, correoId, error);
            }
        }
        return (eventos, correos);
    }

    /// <summary>Toma y procesa un evento listo. Devuelve false si no quedaba ninguno libre.</summary>
    private async Task<bool> ProcesarSiguienteEventoAsync(CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<FlowRideDbContext>();
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);

        var ahora = reloj.GetUtcNow();
        var evento = (await contexto.MensajesOutbox
                .FromSql($"""
                          SELECT * FROM notif.outbox_mensaje
                          WHERE procesado_en IS NULL AND proximo_intento <= {ahora}
                          ORDER BY proximo_intento, ocurrido_en
                          LIMIT 1
                          FOR UPDATE SKIP LOCKED
                          """)
                .ToListAsync(ct))
            .SingleOrDefault();
        if (evento is null)
            return false;

        try
        {
            var notifico = await alcance.ServiceProvider.GetRequiredService<INotificacionesPuertoEntrada>()
                .ProcesarEventoAsync(evento.EventId, evento.TipoEvento, evento.Payload, evento.OcurridoEn, ct);
            evento.MarcarProcesado(reloj.GetUtcNow());
            await contexto.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            EventoProcesado(logger, evento.TipoEvento, evento.EventId, notifico);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            await transaccion.RollbackAsync(ct);
            EventoFallido(logger, evento.TipoEvento, evento.EventId, error);
            await RegistrarFalloAsync(evento.Id, error, ct);
        }
        return true;
    }

    private async Task RegistrarFalloAsync(Guid eventoId, Exception error, CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<FlowRideDbContext>();
        if (await contexto.MensajesOutbox.SingleOrDefaultAsync(m => m.Id == eventoId, ct) is not { } evento)
            return;
        evento.RegistrarFallo($"{error.GetType().Name}: {error.Message}", reloj.GetUtcNow());
        await contexto.SaveChangesAsync(ct);
        if (evento.ProcesadoEn is not null)
            EventoDescartado(logger, evento.TipoEvento, evento.EventId, MensajeOutbox.MaximoIntentos);
    }

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Notificaciones: evento {TipoEvento} {EventId} procesado (notificó: {Notifico}).")]
    private static partial void EventoProcesado(ILogger logger, string tipoEvento, Guid eventId, bool notifico);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "Notificaciones: el evento {TipoEvento} {EventId} falló; se reintentará.")]
    private static partial void EventoFallido(ILogger logger, string tipoEvento, Guid eventId, Exception error);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Warning,
        Message = "Notificaciones: el evento {TipoEvento} {EventId} agotó sus {Intentos} intentos y se descartó.")]
    private static partial void EventoDescartado(ILogger logger, string tipoEvento, Guid eventId, int intentos);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Error, Message = "Notificaciones: no se pudo enviar el correo {CorreoId}.")]
    private static partial void CorreoFallido(ILogger logger, Guid correoId, Exception error);

    [LoggerMessage(EventId = 8005, Level = LogLevel.Error, Message = "Notificaciones: la vuelta falló.")]
    private static partial void VueltaFallida(ILogger logger, Exception error);
}
