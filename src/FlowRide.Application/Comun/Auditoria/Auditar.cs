using FlowRide.Domain.Auditoria;

namespace FlowRide.Application.Comun.Auditoria;

/// <summary>Atajo para los eventos de auditoría de los casos de uso: acción <c>recurso.accion</c> y resultado exitoso.</summary>
internal static class Auditar
{
    public static EventoAuditoria Exito(
        Guid? actorId,
        string recurso,
        string accion,
        Guid recursoId,
        DateTimeOffset ahora,
        IReadOnlyDictionary<string, string?>? antes = null,
        IReadOnlyDictionary<string, string?>? despues = null) =>
        new(actorId, AccionesAuditoria.De(recurso, accion), recurso, recursoId.ToString(), ahora,
            ResultadoAuditoria.Exitoso, valoresAntes: antes, valoresDespues: despues);

    public static Dictionary<string, string?> Valores(params (string Clave, object? Valor)[] valores) =>
        valores.ToDictionary(par => par.Clave, par => par.Valor switch
        {
            null => null,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            var otro => otro.ToString()
        }, StringComparer.Ordinal);
}
