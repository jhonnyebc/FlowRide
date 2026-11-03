namespace FlowRide.Domain.Auditoria;

/// <summary>Verificación de la integridad de la auditoría de un Silo.</summary>
public static class CadenaAuditoria
{
    /// <summary>
    /// Recorre los eventos en orden de Id y devuelve el primero cuya integridad falla: su contenido no coincide con su
    /// hash, o su hash previo no es el hash del evento anterior (evento alterado, borrado o insertado). Null si la
    /// cadena está íntegra.
    /// </summary>
    public static EventoAuditoria? PrimerEventoAlterado(IEnumerable<EventoAuditoria> eventosEnOrden)
    {
        ArgumentNullException.ThrowIfNull(eventosEnOrden);

        var hashAnterior = EventoAuditoria.HashInicial;
        foreach (var evento in eventosEnOrden)
        {
            if (evento.HashPrevio != hashAnterior || !evento.HashValido())
                return evento;
            hashAnterior = evento.Hash;
        }

        return null;
    }
}
