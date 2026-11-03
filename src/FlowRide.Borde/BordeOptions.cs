namespace FlowRide.Borde;

/// <summary>Configuración no secreta del borde (ADR-0012). Los secretos van en el gestor de secretos de la plataforma.</summary>
public sealed class BordeOptions
{
    public const string Seccion = "Borde";

    /// <summary>Cada operador queda en <c>{subdominio}.{DominioBase}</c>; en local, <c>dev.localhost</c> (lo cubre el certificado de desarrollo).</summary>
    public string DominioBase { get; set; } = "dev.localhost";

    /// <summary>Cada cuánto se relee la tabla de ruteo del catálogo, sin reiniciar.</summary>
    public int RecargaRutasSegundos { get; set; } = 30;

    /// <summary>Tamaño máximo de una petición al Panel Web o la API (los documentos del conductor son de hasta 5 MB).</summary>
    public int TamanoMaximoMb { get; set; } = 10;

    /// <summary>Tamaño máximo de un webhook de Meta.</summary>
    public int TamanoWebhookKb { get; set; } = 1024;

    /// <summary>Límite por IP para el Panel Web y la API, en ventanas de un minuto.</summary>
    public int PeticionesPorMinutoPanel { get; set; } = 300;

    /// <summary>Límite por IP para los webhooks (Meta envía desde pocas IP).</summary>
    public int PeticionesPorMinutoWebhook { get; set; } = 1200;

    /// <summary>
    /// Content-Security-Policy de las respuestas. Con el Panel Web servido por el borde debe permitir su Keycloak
    /// (connect-src y form-action) y los estilos en línea de Mantine; en local lo ajusta appsettings.Development.json.
    /// </summary>
    public string PoliticaContenido { get; set; } = "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

    /// <summary>
    /// Carpeta del Panel Web compilado (<c>web/dist</c>), relativa a la del borde. Si está definida, el borde lo sirve en
    /// el subdominio de cada operador (ADR-0019); si no, solo expone la API y la salud de los stacks.
    /// </summary>
    public string? PanelWeb { get; set; }
}
