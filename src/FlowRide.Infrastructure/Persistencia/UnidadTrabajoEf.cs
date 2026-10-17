using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Persistencia;

/// <summary>
/// Unidad de trabajo del Silo. Si hay eventos de auditoría pendientes, los sella dentro de la misma transacción que
/// el cambio: toma un candado consultivo (una sola escritura de la cadena a la vez), lee el último hash y encadena.
/// </summary>
public sealed class UnidadTrabajoEf(FlowRideDbContext contexto) : IUnidadTrabajo
{
    /// <summary>Clave del candado consultivo de la cadena de auditoría ("FlowRide" en ASCII).</summary>
    internal const long CandadoAuditoria = 0x466C6F7752696465;

    public async Task GuardarCambiosAsync(CancellationToken ct)
    {
        var eventos = contexto.ChangeTracker.Entries<EventoAuditoria>()
            .Where(entrada => entrada.State == EntityState.Added)
            .Select(entrada => entrada.Entity)
            .ToList();

        if (eventos.Count == 0)
        {
            await contexto.SaveChangesAsync(ct);
            return;
        }

        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        await contexto.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CandadoAuditoria})", ct);

        var hashAnterior = await contexto.EventosAuditoria
            .OrderByDescending(evento => evento.Id)
            .Select(evento => evento.Hash)
            .FirstOrDefaultAsync(ct) ?? EventoAuditoria.HashInicial;

        foreach (var evento in eventos)
        {
            evento.Sellar(hashAnterior);
            hashAnterior = evento.Hash;
        }

        await contexto.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }
}
