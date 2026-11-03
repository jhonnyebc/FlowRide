using FlowRide.Application.Auditoria.CasosUso;
using FlowRide.Application.Auditoria.Entrada.Dto;
using FlowRide.Application.Auditoria.Entrada.Puertos;
using FlowRide.Application.Comun.CasosUso;

namespace FlowRide.Application.Auditoria.Entrada.Adaptadores;

public sealed class VerificarAuditoriaAdaptadorEntrada(
    ICasoUsoConRetorno<VerificarAuditoriaDominio, ResultadoVerificacionAuditoria> casoUso) : IVerificarAuditoriaPuertoEntrada
{
    public async Task<VerificacionAuditoriaRespuesta> VerificarAsync(CancellationToken ct)
    {
        var resultado = await casoUso.EjecutarAsync(VerificarAuditoriaDominio.Completa, ct);
        return new VerificacionAuditoriaRespuesta(
            resultado.TotalEventos,
            resultado.Integra,
            resultado.PrimerEventoAlterado?.Id,
            resultado.PrimerEventoAlterado is { } alterado
                ? $"La cadena de auditoría está rota desde el evento {alterado.Id}."
                : $"La auditoría está íntegra ({resultado.TotalEventos} eventos).");
    }
}
