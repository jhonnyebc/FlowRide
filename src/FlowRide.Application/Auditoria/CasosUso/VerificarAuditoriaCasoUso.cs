using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;

namespace FlowRide.Application.Auditoria.CasosUso;

/// <summary>Sin parámetros: verifica la cadena completa del Silo.</summary>
public sealed record VerificarAuditoriaDominio
{
    public static VerificarAuditoriaDominio Completa { get; } = new();
}

public sealed record ResultadoVerificacionAuditoria(int TotalEventos, EventoAuditoria? PrimerEventoAlterado)
{
    public bool Integra => PrimerEventoAlterado is null;
}

/// <summary>Comprueba que nadie alteró, borró ni insertó eventos en la auditoría del Silo (ADR-0011).</summary>
public sealed class VerificarAuditoriaCasoUso(IEventoAuditoriaRepositorio auditoria)
    : ICasoUsoConRetorno<VerificarAuditoriaDominio, ResultadoVerificacionAuditoria>
{
    public async Task<ResultadoVerificacionAuditoria> EjecutarAsync(VerificarAuditoriaDominio datos, CancellationToken ct)
    {
        var eventos = await auditoria.ConsultarEnOrdenAsync(ct);
        return new ResultadoVerificacionAuditoria(eventos.Count, CadenaAuditoria.PrimerEventoAlterado(eventos));
    }
}
