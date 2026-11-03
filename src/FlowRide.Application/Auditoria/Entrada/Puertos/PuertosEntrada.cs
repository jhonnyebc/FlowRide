using FlowRide.Application.Auditoria.Entrada.Dto;

namespace FlowRide.Application.Auditoria.Entrada.Puertos;

public interface IVerificarAuditoriaPuertoEntrada
{
    Task<VerificacionAuditoriaRespuesta> VerificarAsync(CancellationToken ct);
}
