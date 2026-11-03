using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Auditoria;

/// <summary>Adaptador de salida de la auditoría del Silo. El sellado y la inserción los hace la unidad de trabajo.</summary>
public sealed class EventoAuditoriaRepositorioAdaptador(FlowRideDbContext contexto) : IEventoAuditoriaRepositorio
{
    public Task CrearAsync(EventoAuditoria entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.EventosAuditoria.Add(entidad);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<EventoAuditoria>> ConsultarEnOrdenAsync(CancellationToken ct) =>
        await contexto.EventosAuditoria.AsNoTracking().OrderBy(evento => evento.Id).ToListAsync(ct);
}
