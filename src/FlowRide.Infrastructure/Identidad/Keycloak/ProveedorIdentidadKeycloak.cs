using System.Text.Json.Nodes;
using FlowRide.Application.Identidad.Salida.Puertos;

namespace FlowRide.Infrastructure.Identidad.Keycloak;

/// <summary>
/// Cuentas del Panel Web en el realm del operador, gestionadas con la cuenta de servicio del propio realm (nunca con
/// el administrador de la plataforma). Forma parte del <c>IdentityProviderAdapter</c> de ADR-0003.
/// </summary>
public sealed class ProveedorIdentidadKeycloak(ClienteKeycloak keycloak, string nombreRealm, IdentidadOptions opciones)
    : IProveedorIdentidad
{
    /// <summary>Vigencia del enlace del correo para definir la contraseña: 72 horas.</summary>
    private const int VigenciaEnlaceSegundos = 72 * 3600;

    private string Usuarios => $"admin/realms/{Uri.EscapeDataString(nombreRealm)}/users";

    public async Task<string> AsegurarUsuarioAsync(string correo, string nombre, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        if (await BuscarPorCorreoAsync(correo, ct) is { } existente)
            return existente;

        // Keycloak pide nombre y apellido: el nombre completo se reparte entre los dos.
        var partes = nombre.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cuenta = new JsonObject
        {
            ["username"] = correo,
            ["email"] = correo,
            ["firstName"] = partes[0],
            ["lastName"] = partes.Length > 1 ? partes[1] : partes[0],
            ["enabled"] = true,
            ["emailVerified"] = true,
            ["requiredActions"] = new JsonArray("UPDATE_PASSWORD")
        };
        await keycloak.CrearAsync(Usuarios, cuenta, ct);

        return await BuscarPorCorreoAsync(correo, ct)
               ?? throw new InvalidOperationException($"Keycloak no creó la cuenta de {correo}.");
    }

    public Task EnviarConfiguracionContrasenaAsync(string identidadExternaId, CancellationToken ct)
    {
        var destino = Uri.EscapeDataString(opciones.UrlPanel.TrimEnd('/') + "/");
        return keycloak.EjecutarAsync(HttpMethod.Put,
            $"{Usuarios}/{Uri.EscapeDataString(identidadExternaId)}/execute-actions-email" +
            $"?client_id={ConstantesKeycloak.ClientePanel}&redirect_uri={destino}&lifespan={VigenciaEnlaceSegundos}",
            new JsonArray("UPDATE_PASSWORD"), ct);
    }

    public Task HabilitarAsync(string identidadExternaId, CancellationToken ct) =>
        CambiarHabilitadoAsync(identidadExternaId, true, ct);

    public async Task DeshabilitarAsync(string identidadExternaId, CancellationToken ct)
    {
        await CambiarHabilitadoAsync(identidadExternaId, false, ct);
        await keycloak.EjecutarAsync(HttpMethod.Post, $"{Usuarios}/{Uri.EscapeDataString(identidadExternaId)}/logout", null, ct);
    }

    private async Task CambiarHabilitadoAsync(string identidadExternaId, bool habilitado, CancellationToken ct)
    {
        // Se envía la representación completa: una parcial puede borrar atributos del perfil.
        var ruta = $"{Usuarios}/{Uri.EscapeDataString(identidadExternaId)}";
        var cuenta = await keycloak.ObtenerAsync(ruta, ct)
                     ?? throw new InvalidOperationException($"La cuenta {identidadExternaId} no existe en el realm {nombreRealm}.");
        cuenta["enabled"] = habilitado;
        await keycloak.EjecutarAsync(HttpMethod.Put, ruta, cuenta, ct);
    }

    private async Task<string?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
    {
        var encontrados = await keycloak.ListarAsync($"{Usuarios}?email={Uri.EscapeDataString(correo)}&exact=true", ct);
        return encontrados.FirstOrDefault()?["id"]?.GetValue<string>();
    }
}
