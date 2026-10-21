namespace FlowRide.Application.Identidad.Entrada.Dto;

// Peticiones tal como llegan del adaptador de infraestructura (cuerpo HTTP o comando de la CLI): todo opcional, el
// adaptador de entrada valida los campos obligatorios.

public sealed record CrearUsuarioPeticion
{
    public string? Nombre { get; init; }
    public string? Correo { get; init; }
    public string? TelefonoWhatsApp { get; init; }

    /// <summary><c>Administrador</c> u <c>OperadorLogistico</c>. El administrador inicial lo ignora.</summary>
    public string? Rol { get; init; }
}

public sealed record CambiarRolUsuarioPeticion
{
    public string? Rol { get; init; }
}

/// <summary>Datos de contacto de un usuario del Panel Web (RF-24): los tres son obligatorios.</summary>
public sealed record ActualizarUsuarioPeticion
{
    public string? Nombre { get; init; }
    public string? Correo { get; init; }
    public string? TelefonoWhatsApp { get; init; }
}
