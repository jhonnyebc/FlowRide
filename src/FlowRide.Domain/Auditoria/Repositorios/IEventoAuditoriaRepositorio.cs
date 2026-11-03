using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Auditoria.Repositorios;

/// <summary>
/// Auditoría del Silo: solo crear y consultar (no hay modificar ni eliminar). El evento se sella y se inserta al
/// confirmar la unidad de trabajo, en la misma transacción que el cambio que registra.
/// </summary>
public interface IEventoAuditoriaRepositorio : ICrearRepositorio<EventoAuditoria>
{
    /// <summary>Todos los eventos en orden de Id, para verificar la cadena.</summary>
    Task<IReadOnlyList<EventoAuditoria>> ConsultarEnOrdenAsync(CancellationToken ct);
}
