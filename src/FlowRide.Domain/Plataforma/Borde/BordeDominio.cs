namespace FlowRide.Domain.Plataforma.Borde;

// Datos de entrada y resultados de los casos de uso del borde común (ADR-0012) y del registro de rutas (RF-03).

public sealed record RegistrarRutaOperadorDominio(string OperadorId, string Subdominio, string UrlStack, string? PhoneNumberId);

/// <summary>Cargar la tabla de ruteo de los operadores activos.</summary>
public sealed record CargarTablaRutasDominio
{
    public static CargarTablaRutasDominio Instancia { get; } = new();
}

/// <summary>Un webhook de Meta tal como llegó (cuerpo sin tocar: la firma se calcula sobre él) y la tabla vigente.</summary>
public sealed record EnrutarWebhookDominio(string Cuerpo, string? Firma, TablaRutas Tabla);

/// <summary>
/// A qué stacks se reenvía el webhook. Con la firma inválida o el formato roto no hay destinos. Los números sin
/// operador se informan para registrarlos (configuración incompleta), sin reenviar.
/// </summary>
public sealed record ResultadoEnrutamientoWebhook(
    bool FirmaValida,
    bool FormatoValido,
    IReadOnlyList<RutaOperador> Destinos,
    IReadOnlyList<string> NumerosSinOperador)
{
    public static ResultadoEnrutamientoWebhook FirmaInvalida { get; } = new(false, false, [], []);

    public static ResultadoEnrutamientoWebhook FormatoInvalido { get; } = new(true, false, [], []);
}
