using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using FlowRide.Infrastructure.Identidad;
using FlowRide.Infrastructure.Identidad.Keycloak;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Keycloak real (la misma versión que docker-compose) y Mailpit en una red propia, como en local: Keycloak envía
/// los correos de contraseña a Mailpit y la prueba los lee por su API.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    public const string HostMailpit = "mailpit";
    public const string Usuario = "admin";
    public const string Contrasena = "admin-de-pruebas";

    private readonly INetwork _red;
    private readonly IContainer _mailpit;
    private readonly IContainer _keycloak;
    private readonly HttpClient _http = new();
    private ClienteKeycloak? _administrador;

    public KeycloakFixture()
    {
        _red = new NetworkBuilder().Build();
        _mailpit = new ContainerBuilder("axllent/mailpit:v1.27.7")
            .WithNetwork(_red)
            .WithNetworkAliases(HostMailpit)
            .WithPortBinding(8025, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(peticion => peticion.ForPort(8025).ForPath("/")))
            .Build();
        _keycloak = new ContainerBuilder("quay.io/keycloak/keycloak:26.3.3")
            .WithNetwork(_red)
            .WithCommand("start-dev")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", Usuario)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", Contrasena)
            .WithEnvironment("KC_HEALTH_ENABLED", "true")
            .WithPortBinding(8080, assignRandomHostPort: true)
            .WithPortBinding(9000, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(peticion => peticion.ForPort(9000).ForPath("/health/ready")))
            .Build();
    }

    public string UrlBase => $"http://{_keycloak.Hostname}:{_keycloak.GetMappedPublicPort(8080)}";

    private string UrlMailpit => $"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}";

    /// <summary>API de administración con la cuenta de la plataforma, para inspeccionar los realms.</summary>
    public ClienteKeycloak Administrador =>
        _administrador ??= new ClienteKeycloak(UrlBase, CredencialesKeycloak.Administrador(Usuario, Contrasena), TimeProvider.System);

    public async Task InitializeAsync()
    {
        await _red.CreateAsync();
        await _mailpit.StartAsync();
        await _keycloak.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _administrador?.Dispose();
        _http.Dispose();
        await _keycloak.DisposeAsync();
        await _mailpit.DisposeAsync();
        await _red.DisposeAsync();
    }

    /// <summary>Asuntos de los correos que Mailpit recibió para esa dirección.</summary>
    public async Task<IReadOnlyList<string>> CorreosParaAsync(string direccion)
    {
        var respuesta = await _http.GetFromJsonAsync<JsonNode>($"{UrlMailpit}/api/v1/messages?limit=500");
        return (respuesta?["messages"]?.AsArray() ?? [])
            .Where(mensaje => mensaje!["To"]!.AsArray().Any(destino =>
                string.Equals(destino!["Address"]!.GetValue<string>(), direccion, StringComparison.OrdinalIgnoreCase)))
            .Select(mensaje => mensaje!["Subject"]!.GetValue<string>())
            .ToList();
    }

    public async Task<JsonObject?> CuentaAsync(string realm, string correo)
    {
        var cuentas = await Administrador.ListarAsync($"admin/realms/{realm}/users?email={Uri.EscapeDataString(correo)}&exact=true", default);
        return cuentas.FirstOrDefault()?.AsObject();
    }

    /// <summary>
    /// Simula que el usuario siguió el enlace del correo y definió su contraseña, y obtiene un token como el del
    /// Panel Web. Para no automatizar el navegador, habilita en el cliente del panel el flujo de contraseña directa.
    /// </summary>
    public async Task<string> IniciarSesionAsync(string realm, string correo, string contrasena = "Clave-de-prueba-1")
    {
        var cuenta = await CuentaAsync(realm, correo) ?? throw new InvalidOperationException($"No existe la cuenta {correo}.");
        var id = cuenta["id"]!.GetValue<string>();
        await Administrador.EjecutarAsync(HttpMethod.Put, $"admin/realms/{realm}/users/{id}/reset-password",
            new JsonObject { ["type"] = "password", ["value"] = contrasena, ["temporary"] = false }, default);
        var actualizada = (await Administrador.ObtenerAsync($"admin/realms/{realm}/users/{id}", default))!.AsObject();
        actualizada["requiredActions"] = new JsonArray();
        await Administrador.EjecutarAsync(HttpMethod.Put, $"admin/realms/{realm}/users/{id}", actualizada, default);

        var panel = (await Administrador.ListarAsync($"admin/realms/{realm}/clients?clientId={ConstantesKeycloak.ClientePanel}", default))[0]!.AsObject();
        panel["directAccessGrantsEnabled"] = true;
        await Administrador.EjecutarAsync(HttpMethod.Put, $"admin/realms/{realm}/clients/{panel["id"]!.GetValue<string>()}", panel, default);

        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = ConstantesKeycloak.ClientePanel,
            ["username"] = correo,
            ["password"] = contrasena,
            ["scope"] = "openid"
        });
        using var respuesta = await _http.PostAsync($"{UrlBase}/realms/{realm}/protocol/openid-connect/token", formulario);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonNode>();
        return cuerpo?["access_token"]?.GetValue<string>()
               ?? throw new InvalidOperationException($"Keycloak no entregó el token: {cuerpo?.ToJsonString()}");
    }
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionKeycloak : ICollectionFixture<PostgresFixture>, ICollectionFixture<KeycloakFixture>
{
    public const string Nombre = "keycloak";
}
