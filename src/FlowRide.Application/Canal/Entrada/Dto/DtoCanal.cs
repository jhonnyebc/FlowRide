namespace FlowRide.Application.Canal.Entrada.Dto;

/// <summary>Mensaje simulado (solo en desarrollo, con el proveedor simulado).</summary>
public sealed record MensajeSimuladoPeticion
{
    public string? Telefono { get; init; }
    public string? Texto { get; init; }
}

/// <summary>Resultado del webhook para el adaptador HTTP.</summary>
public sealed record WebhookRespuesta(bool FirmaValida, bool FormatoValido, int Nuevos, int Duplicados, int EstadosActualizados, int Ignorados);
