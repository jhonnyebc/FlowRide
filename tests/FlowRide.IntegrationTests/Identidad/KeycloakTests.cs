using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Aprovisionamiento;
using FlowRide.Domain.Identidad;
using FlowRide.Infrastructure.Identidad;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Identidad;

/// <summary>
/// Identidad contra un Keycloak real (ADR-0003, ADR-0018): realm por operador desde la plantilla, primer administrador
/// con su correo de contraseña, tokens del realm aceptados solo por el stack de su operador y revocación.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionKeycloak.Nombre)]
public class KeycloakTests(PostgresFixture postgres, KeycloakFixture keycloak) : IAsyncLifetime
{
    private EntornoAprovisionamiento _entorno = null!;

    public async Task InitializeAsync() => _entorno = await EntornoAprovisionamiento.CrearAsync(postgres, keycloak);

    public async Task DisposeAsync() => await _entorno.DisposeAsync();

    private static string TenantNuevo() => $"kc-{Guid.NewGuid():N}"[..14];

    private async Task<(int Codigo, string Salida)> CrearOperadorAsync(string tenant, string correoAdministrador)
    {
        var argumentos = new[]
        {
            "crear-operador", "--id", tenant, "--razon-social", $"Operador {tenant}", "--nit", $"nit-{tenant}",
            "--correo", $"{tenant}@ops.co", "--admin-nombre", "Ana María Rojas", "--admin-correo", correoAdministrador,
            "--admin-telefono", $"+57{Random.Shared.Next(100_000_000, 999_999_999)}"
        };
        Assert.True(Comando.TryParse(argumentos, out var comando, out var error), error);

        using var salida = new StringWriter();
        var codigo = await _entorno.UsarAsync(servicios => Comandos.EjecutarAsync(comando, servicios, salida, CancellationToken.None));
        return (codigo, salida.ToString());
    }

    private async Task<FlowRideApiFactory> ApiDeAsync(string tenant) =>
        new(await _entorno.CadenaDelSiloAsync(tenant), tenant)
        {
            UrlKeycloak = keycloak.UrlBase,
            SecretoCliente = await _entorno.Secretos.ObtenerAsync($"flowride-{tenant}", ClavesSecretos.SecretoClienteIdentidad, CancellationToken.None)
        };

    private static HttpClient Cliente(FlowRideApiFactory api, string token)
    {
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [Fact]
    public async Task ElRealm_SeCreaDesdeLaPlantilla_YReaprovisionarEsIdempotente()
    {
        var tenant = TenantNuevo();
        var realm = $"flowride-{tenant}";

        await _entorno.AprovisionarAsync(tenant);
        await _entorno.AprovisionarAsync(tenant);

        var configuracion = await keycloak.Administrador.ObtenerAsync($"admin/realms/{realm}", CancellationToken.None);
        Assert.Equal(300, configuracion!["accessTokenLifespan"]!.GetValue<int>());
        Assert.False(configuracion["registrationAllowed"]!.GetValue<bool>());
        Assert.True(configuracion["resetPasswordAllowed"]!.GetValue<bool>());

        var panel = (await keycloak.Administrador.ListarAsync($"admin/realms/{realm}/clients?clientId={ConstantesKeycloak.ClientePanel}", CancellationToken.None)).Single()!;
        Assert.True(panel["publicClient"]!.GetValue<bool>());
        Assert.Equal("S256", panel["attributes"]!["pkce.code.challenge.method"]!.GetValue<string>());
        var mappers = panel["protocolMappers"]!.AsArray();
        Assert.Equal(2, mappers.Count);
        Assert.Equal(tenant, mappers.Single(m => m!["name"]!.GetValue<string>() == "tenant_id")!["config"]!["claim.value"]!.GetValue<string>());

        var servicio = (await keycloak.Administrador.ListarAsync($"admin/realms/{realm}/clients?clientId={ConstantesKeycloak.ClienteServicio}", CancellationToken.None)).Single()!;
        Assert.True(servicio["serviceAccountsEnabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task CrearOperadorConAdministrador_CreaSuCuentaYLeEnviaElCorreoParaDefinirLaContrasena()
    {
        var tenant = TenantNuevo();
        var correo = $"admin@{tenant}.co";

        var (codigo, salida) = await CrearOperadorAsync(tenant, correo);
        var (codigoOtraVez, salidaOtraVez) = await CrearOperadorAsync(tenant, correo);

        Assert.Equal(0, codigo);
        Assert.Contains($"realm 'flowride-{tenant}'", salida, StringComparison.Ordinal);
        Assert.Contains("Administrador creado", salida, StringComparison.Ordinal);
        Assert.Equal(0, codigoOtraVez);
        Assert.Contains("ya existía", salidaOtraVez, StringComparison.Ordinal);

        var cuenta = await keycloak.CuentaAsync($"flowride-{tenant}", correo);
        Assert.NotNull(cuenta);
        Assert.Contains("UPDATE_PASSWORD", cuenta["requiredActions"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Single(await keycloak.CorreosParaAsync(correo));
    }

    [Fact]
    public async Task ElTokenDelRealm_AbreLaApiDeSuOperador_PeroNoLaDeOtro()
    {
        var tenantA = TenantNuevo();
        var tenantB = TenantNuevo();
        var correo = $"admin@{tenantA}.co";
        await CrearOperadorAsync(tenantA, correo);
        await CrearOperadorAsync(tenantB, $"admin@{tenantB}.co");
        var token = await keycloak.IniciarSesionAsync($"flowride-{tenantA}", correo);

        await using var apiA = await ApiDeAsync(tenantA);
        await using var apiB = await ApiDeAsync(tenantB);
        var yo = await Cliente(apiA, token).GetFromJsonAsync<UsuarioAutenticadoRespuesta>("/api/usuarios/yo");
        var enOtroOperador = await Cliente(apiB, token).GetAsync("/api/usuarios/yo");

        Assert.Equal(correo, yo!.Correo);
        Assert.Equal(RolesSistema.Administrador, yo.Rol);
        Assert.Equal(HttpStatusCode.Unauthorized, enOtroOperador.StatusCode);
    }

    [Fact]
    public async Task ElAdministrador_CreaYDesactivaUnUsuario_YKeycloakLoRefleja()
    {
        var tenant = TenantNuevo();
        var realm = $"flowride-{tenant}";
        var correoAdministrador = $"admin@{tenant}.co";
        var correoOperador = $"laura@{tenant}.co";
        await CrearOperadorAsync(tenant, correoAdministrador);
        await using var api = await ApiDeAsync(tenant);
        var administrador = Cliente(api, await keycloak.IniciarSesionAsync(realm, correoAdministrador));

        var creado = await administrador.PostAsJsonAsync("/api/usuarios", new CrearUsuarioPeticion
        {
            Nombre = "Laura Gómez",
            Correo = correoOperador,
            TelefonoWhatsApp = $"+57{Random.Shared.Next(100_000_000, 999_999_999)}",
            Rol = RolesSistema.OperadorLogistico
        });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var usuario = (await creado.Content.ReadFromJsonAsync<UsuarioRespuesta>())!;
        Assert.Single(await keycloak.CorreosParaAsync(correoOperador));

        var operador = Cliente(api, await keycloak.IniciarSesionAsync(realm, correoOperador));
        Assert.Equal(HttpStatusCode.OK, (await operador.GetAsync("/api/usuarios/yo")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await administrador.PostAsync($"/api/usuarios/{usuario.Id}/desactivar", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await operador.GetAsync("/api/usuarios/yo")).StatusCode);
        var cuenta = await keycloak.CuentaAsync(realm, correoOperador);
        Assert.False(cuenta!["enabled"]!.GetValue<bool>());
    }
}
