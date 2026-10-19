namespace FlowRide.Application.Notificaciones.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura: todo opcional, el adaptador de entrada valida.

public sealed record BusquedaAlertasPeticion
{
    public bool? SoloNoLeidas { get; init; }
    public int? Pagina { get; init; }
    public int? TamanoPagina { get; init; }
}

public sealed record PlantillaPeticion
{
    /// <summary>Nombre de la plantilla en la cuenta de WhatsApp Business del operador.</summary>
    public string? Nombre { get; init; }

    /// <summary>Código de idioma de Meta, p. ej. <c>es_CO</c>.</summary>
    public string? Idioma { get; init; }

    /// <summary>Pendiente, Aprobada o Rechazada.</summary>
    public string? Estado { get; init; }
}

public sealed record PlantillaRespuesta(string Clave, string Nombre, string Idioma, string Estado, IReadOnlyList<string> Variables,
    DateTimeOffset ModificadaEn);

public sealed record AlertaLeidaRespuesta(Guid Id, DateTimeOffset? LeidaEn);
