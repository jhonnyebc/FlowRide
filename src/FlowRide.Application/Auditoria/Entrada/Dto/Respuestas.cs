namespace FlowRide.Application.Auditoria.Entrada.Dto;

public sealed record VerificacionAuditoriaRespuesta(int TotalEventos, bool Integra, long? PrimerEventoAlteradoId, string Mensaje);
