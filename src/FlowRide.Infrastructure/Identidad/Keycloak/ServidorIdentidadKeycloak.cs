using System.Text.Json;
using System.Text.Json.Nodes;
using FlowRide.Application.Tenancy.Salida.Puertos;

namespace FlowRide.Infrastructure.Identidad.Keycloak;

/// <summary>Servidor SMTP que usa Keycloak para los correos de contraseña (en local, Mailpit).</summary>
public sealed class SmtpKeycloakOptions
{
    public const string Seccion = "FlowRide:Keycloak:Smtp";

    public string Host { get; set; } = "mailpit";
    public int Puerto { get; set; } = 1025;
    public string Remitente { get; set; } = "no-responder@flowride.local";
}

/// <summary>
/// Crea o actualiza el realm de un operador a partir de <c>plantilla-realm.json</c> (versionada y sin secretos,
/// ADR-0003). Usa la cuenta de administración de la plataforma; cada paso es idempotente.
/// </summary>
public sealed class ServidorIdentidadKeycloak(
    ClienteKeycloak keycloak,
    IdentidadOptions identidad,
    SmtpKeycloakOptions smtp) : IServidorIdentidad
{
    private const string RecursoPlantilla = "FlowRide.plantilla-realm.json";

    public async Task AsegurarRealmAsync(string nombreRealm, string tenantId, string secretoClienteServicio, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreRealm);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretoClienteServicio);

        var plantilla = Plantilla(nombreRealm, tenantId);
        var clientes = plantilla["clients"]!.AsArray().Select(cliente => cliente!.AsObject()).ToList();
        plantilla.Remove("clients");

        // 1. Realm y su configuración (duración de tokens, SMTP, recuperación de contraseña...).
        var realm = $"admin/realms/{Uri.EscapeDataString(nombreRealm)}";
        if (await keycloak.ObtenerAsync(realm, ct) is null)
            await keycloak.CrearAsync("admin/realms", plantilla.DeepClone(), ct);
        await keycloak.EjecutarAsync(HttpMethod.Put, realm, plantilla, ct);

        // 2. Clientes del Panel Web y de la API, con sus mappers (tenant_id y audiencia).
        foreach (var plantillaCliente in clientes)
        {
            var cliente = plantillaCliente.DeepClone().AsObject();
            var esServicio = cliente["clientId"]!.GetValue<string>() == ConstantesKeycloak.ClienteServicio;
            if (esServicio)
                cliente["secret"] = secretoClienteServicio;
            else if (identidad.UrlPanelDe(tenantId) is { } panelDelBorde)
                AgregarOrigenDelPanel(cliente, panelDelBorde);

            var clienteId = await AsegurarClienteAsync(realm, cliente, ct);
            if (esServicio)
                await AsegurarRolesCuentaServicioAsync(realm, clienteId, ct);
        }
    }

    /// <summary>El Panel Web servido por el borde es otro origen del mismo cliente: destino de regreso, CORS y cierre de sesión.</summary>
    private static void AgregarOrigenDelPanel(JsonObject cliente, string origen)
    {
        cliente["redirectUris"]!.AsArray().Add($"{origen}/*");
        cliente["webOrigins"]!.AsArray().Add(origen);
        var atributos = cliente["attributes"]!.AsObject();
        // Keycloak separa con "##" los destinos de regreso tras cerrar sesión.
        atributos["post.logout.redirect.uris"] = $"{atributos["post.logout.redirect.uris"]!.GetValue<string>()}##{origen}/*";
    }

    private async Task<string> AsegurarClienteAsync(string realm, JsonObject cliente, CancellationToken ct)
    {
        var clientId = cliente["clientId"]!.GetValue<string>();
        var mappers = cliente["protocolMappers"]?.AsArray().Select(mapper => mapper!.AsObject()).ToList() ?? [];
        cliente.Remove("protocolMappers");

        var existente = await BuscarClienteAsync(realm, clientId, ct);
        if (existente is null)
        {
            await keycloak.CrearAsync($"{realm}/clients", cliente.DeepClone(), ct);
            existente = await BuscarClienteAsync(realm, clientId, ct)
                        ?? throw new InvalidOperationException($"Keycloak no creó el cliente {clientId}.");
        }

        var id = existente["id"]!.GetValue<string>();
        cliente["id"] = id;
        await keycloak.EjecutarAsync(HttpMethod.Put, $"{realm}/clients/{id}", cliente, ct);

        // Actualizar un cliente no toca sus mappers: se sincronizan uno a uno por nombre.
        var actuales = await keycloak.ListarAsync($"{realm}/clients/{id}/protocol-mappers/models", ct);
        foreach (var mapper in mappers)
        {
            var nombre = mapper["name"]!.GetValue<string>();
            var actual = actuales.FirstOrDefault(m => m?["name"]?.GetValue<string>() == nombre);
            if (actual is null)
            {
                await keycloak.CrearAsync($"{realm}/clients/{id}/protocol-mappers/models", mapper.DeepClone(), ct);
                continue;
            }

            var actualizado = mapper.DeepClone().AsObject();
            var mapperId = actual["id"]!.GetValue<string>();
            actualizado["id"] = mapperId;
            await keycloak.EjecutarAsync(HttpMethod.Put, $"{realm}/clients/{id}/protocol-mappers/models/{mapperId}", actualizado, ct);
        }

        return id;
    }

    /// <summary>La cuenta de servicio de la API solo puede gestionar las cuentas de su propio realm.</summary>
    private async Task AsegurarRolesCuentaServicioAsync(string realm, string clienteId, CancellationToken ct)
    {
        var cuentaServicio = await keycloak.ObtenerAsync($"{realm}/clients/{clienteId}/service-account-user", ct)
                             ?? throw new InvalidOperationException("El cliente de servicio no tiene cuenta de servicio.");
        var gestion = await BuscarClienteAsync(realm, "realm-management", ct)
                      ?? throw new InvalidOperationException("El realm no tiene el cliente realm-management.");
        var gestionId = gestion["id"]!.GetValue<string>();

        var roles = new JsonArray();
        foreach (var nombre in ConstantesKeycloak.RolesCuentaServicio)
        {
            roles.Add(await keycloak.ObtenerAsync($"{realm}/clients/{gestionId}/roles/{nombre}", ct)
                      ?? throw new InvalidOperationException($"El realm no tiene el rol {nombre}."));
        }

        var usuarioId = cuentaServicio["id"]!.GetValue<string>();
        await keycloak.EjecutarAsync(HttpMethod.Post, $"{realm}/users/{usuarioId}/role-mappings/clients/{gestionId}", roles, ct);
    }

    private async Task<JsonNode?> BuscarClienteAsync(string realm, string clientId, CancellationToken ct) =>
        (await keycloak.ListarAsync($"{realm}/clients?clientId={Uri.EscapeDataString(clientId)}", ct)).FirstOrDefault();

    private JsonObject Plantilla(string nombreRealm, string tenantId)
    {
        using var recurso = typeof(ServidorIdentidadKeycloak).Assembly.GetManifestResourceStream(RecursoPlantilla)
                            ?? throw new InvalidOperationException($"Falta el recurso {RecursoPlantilla}.");
        using var lector = new StreamReader(recurso);
        var texto = lector.ReadToEnd();

        var valores = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["REALM"] = nombreRealm,
            ["TENANT_ID"] = tenantId,
            ["URL_PANEL"] = identidad.UrlPanel.TrimEnd('/'),
            ["SMTP_HOST"] = smtp.Host,
            ["SMTP_PUERTO"] = smtp.Puerto.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SMTP_REMITENTE"] = smtp.Remitente
        };
        foreach (var (clave, valor) in valores)
            texto = texto.Replace("${" + clave + "}", JsonEncodedText.Encode(valor).Value, StringComparison.Ordinal);

        return JsonNode.Parse(texto)!.AsObject();
    }
}
