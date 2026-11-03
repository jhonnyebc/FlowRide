using FlowRide.Application.Horarios.Entrada.Puertos;
using FlowRide.Application.Horarios.Salida.Puertos;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowRide.Infrastructure.Horarios;

/// <summary>Eventos futuros del Silo, rastreados para recalcular su instante derivado.</summary>
public sealed class HorariosFuturosEf(FlowRideDbContext contexto) : IHorariosFuturos
{
    private static readonly EstadoSolicitud[] Activas =
        [EstadoSolicitud.Pendiente, EstadoSolicitud.Asignada, EstadoSolicitud.SinCandidatos, EstadoSolicitud.EnCurso];

    private static readonly EstadoAsignacion[] Vigentes = [EstadoAsignacion.Asignada, EstadoAsignacion.Aceptada, EstadoAsignacion.EnCurso];

    public async Task<IReadOnlyList<(SolicitudViaje Solicitud, IReadOnlyList<Asignacion> Vigentes)>> SolicitudesAsync(
        DateTimeOffset desde, CancellationToken ct)
    {
        var solicitudes = await contexto.Solicitudes
            .Where(s => Activas.Contains(s.Estado) && s.FechaHoraServicio >= desde)
            .ToListAsync(ct);
        var ids = solicitudes.Select(s => s.Id).ToList();
        var asignaciones = (await contexto.Asignaciones
                .Where(a => ids.Contains(a.SolicitudId) && Vigentes.Contains(a.Estado))
                .ToListAsync(ct))
            .ToLookup(a => a.SolicitudId);
        return solicitudes.Select(s => (s, (IReadOnlyList<Asignacion>)asignaciones[s.Id].ToList())).ToList();
    }

    public async Task<IReadOnlyList<Turno>> TurnosAsync(DateTimeOffset desde, CancellationToken ct) =>
        await contexto.Set<Turno>()
            .Where(t => (t.Estado == EstadoTurno.Programado || t.Estado == EstadoTurno.Activo) && t.Franja.Fin >= desde)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Parada>> ParadasAsync(DateTimeOffset desde, CancellationToken ct) =>
        await contexto.Set<Parada>().Where(p => p.HoraReal == null && p.HoraEstimada >= desde).ToListAsync(ct);
}

/// <summary>
/// Al arrancar el stack, recalcula los instantes de los eventos futuros: la base de zonas horarias se actualiza con el
/// sistema y con .NET, y llega con un reinicio. Si falla, el stack arranca igual (los instantes guardados siguen
/// siendo los de la última regla conocida).
/// </summary>
public sealed partial class RecalculoHorariosAlIniciar(IServiceScopeFactory alcances, ILogger<RecalculoHorariosAlIniciar> registro)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var alcance = alcances.CreateAsyncScope();
            var cambiados = await alcance.ServiceProvider.GetRequiredService<IHorariosPuertoEntrada>().RecalcularAsync(cancellationToken);
            if (cambiados > 0)
                Recalculados(registro, cambiados);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Fallo(registro, error);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cambiaron las reglas de una zona horaria: {Cambiados} evento(s) futuro(s) recalculado(s).")]
    private static partial void Recalculados(ILogger registro, int cambiados);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudieron recalcular los horarios futuros al arrancar.")]
    private static partial void Fallo(ILogger registro, Exception error);
}
