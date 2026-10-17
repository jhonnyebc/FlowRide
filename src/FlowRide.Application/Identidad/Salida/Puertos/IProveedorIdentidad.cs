namespace FlowRide.Application.Identidad.Salida.Puertos;

/// <summary>
/// Proveedor OIDC del operador (Keycloak, un realm por operador, ADR-0003) visto desde su stack. Solo gestiona las
/// cuentas del Panel Web: el rol, el estado y el tenant los decide FlowRide, no el proveedor.
/// </summary>
public interface IProveedorIdentidad
{
    /// <summary>
    /// Crea la cuenta con ese correo, o devuelve la que ya existe (reintento tras un fallo), y retorna su identificador
    /// externo (claim <c>sub</c>). La cuenta queda sin contraseña hasta que el usuario la defina.
    /// </summary>
    Task<string> AsegurarUsuarioAsync(string correo, string nombre, CancellationToken ct);

    /// <summary>Envía al usuario el correo para definir su contraseña (también sirve para recuperarla, RF-23).</summary>
    Task EnviarConfiguracionContrasenaAsync(string identidadExternaId, CancellationToken ct);

    Task HabilitarAsync(string identidadExternaId, CancellationToken ct);

    /// <summary>Deshabilita la cuenta y cierra sus sesiones: no puede iniciar sesión ni renovar tokens.</summary>
    Task DeshabilitarAsync(string identidadExternaId, CancellationToken ct);
}
