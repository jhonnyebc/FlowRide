using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace FlowRide.Infrastructure.Identidad.Keycloak;

/// <summary>Con qué se autentica un cliente contra la API de administración de Keycloak.</summary>
public sealed record CredencialesKeycloak(
    string Realm,
    string ClientId,
    string? SecretoCliente = null,
    string? Usuario = null,
    string? Contrasena = null)
{
    /// <summary>Administrador de la plataforma (realm master): solo lo usa la herramienta de aprovisionamiento.</summary>
    public static CredencialesKeycloak Administrador(string usuario, string contrasena) =>
        new("master", "admin-cli", Usuario: usuario, Contrasena: contrasena);

    /// <summary>Cuenta de servicio del realm de un operador: solo gestiona las cuentas de ese realm.</summary>
    public static CredencialesKeycloak CuentaServicio(string realm, string secreto) =>
        new(realm, ConstantesKeycloak.ClienteServicio, SecretoCliente: secreto);

    public override string ToString() => $"{ClientId}@{Realm}";
}

/// <summary>
/// Cliente HTTP mínimo de la API REST de administración de Keycloak: obtiene y renueva el token de acceso y
/// convierte las respuestas fallidas en errores legibles (sin secretos).
/// </summary>
public sealed class ClienteKeycloak : IDisposable
{
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly CredencialesKeycloak _credenciales;
    private readonly TimeProvider _reloj;
    private readonly SemaphoreSlim _candado = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiraEn;

    public ClienteKeycloak(string urlBase, CredencialesKeycloak credenciales, TimeProvider reloj)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlBase);
        _credenciales = credenciales ?? throw new ArgumentNullException(nameof(credenciales));
        _reloj = reloj ?? throw new ArgumentNullException(nameof(reloj));
        _http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            BaseAddress = new Uri(urlBase.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>GET que devuelve null si el recurso no existe (404).</summary>
    public async Task<JsonNode?> ObtenerAsync(string ruta, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null, ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        await AsegurarExitoAsync(respuesta, $"GET {ruta}", ct);
        return await respuesta.Content.ReadFromJsonAsync<JsonNode>(ct);
    }

    public async Task<JsonArray> ListarAsync(string ruta, CancellationToken ct) =>
        await ObtenerAsync(ruta, ct) as JsonArray ?? [];

    /// <summary>Envía la petición y falla si Keycloak no responde 2xx. Devuelve la cabecera Location, si la hay.</summary>
    public async Task<Uri?> EjecutarAsync(HttpMethod metodo, string ruta, JsonNode? cuerpo, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(metodo, ruta, cuerpo, ct);
        await AsegurarExitoAsync(respuesta, $"{metodo} {ruta}", ct);
        return respuesta.Headers.Location;
    }

    /// <summary>Como <see cref="EjecutarAsync"/>, pero devuelve false si el recurso ya existía (409).</summary>
    public async Task<bool> CrearAsync(string ruta, JsonNode cuerpo, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, ruta, cuerpo, ct);
        if (respuesta.StatusCode == HttpStatusCode.Conflict)
            return false;
        await AsegurarExitoAsync(respuesta, $"POST {ruta}", ct);
        return true;
    }

    public async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, JsonNode? cuerpo, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta.TrimStart('/'));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo);
        return await _http.SendAsync(peticion, ct);
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await _candado.WaitAsync(ct);
        try
        {
            if (_token is not null && _reloj.GetUtcNow() < _expiraEn - MargenRenovacion)
                return _token;

            var formulario = new Dictionary<string, string> { ["client_id"] = _credenciales.ClientId };
            if (_credenciales.Usuario is not null)
            {
                formulario["grant_type"] = "password";
                formulario["username"] = _credenciales.Usuario;
                formulario["password"] = _credenciales.Contrasena ?? string.Empty;
            }
            else
            {
                formulario["grant_type"] = "client_credentials";
                formulario["client_secret"] = _credenciales.SecretoCliente ?? string.Empty;
            }

            using var contenido = new FormUrlEncodedContent(formulario);
            using var respuesta = await _http.PostAsync(
                $"realms/{Uri.EscapeDataString(_credenciales.Realm)}/protocol/openid-connect/token", contenido, ct);
            await AsegurarExitoAsync(respuesta, $"obtener el token de {_credenciales}", ct);

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonNode>(ct);
            _token = cuerpo?["access_token"]?.GetValue<string>()
                     ?? throw new InvalidOperationException($"Keycloak no devolvió un token para {_credenciales}.");
            _expiraEn = _reloj.GetUtcNow().AddSeconds(cuerpo["expires_in"]?.GetValue<int>() ?? 60);
            return _token;
        }
        finally
        {
            _candado.Release();
        }
    }

    private static async Task AsegurarExitoAsync(HttpResponseMessage respuesta, string operacion, CancellationToken ct)
    {
        if (respuesta.IsSuccessStatusCode)
            return;

        var detalle = await respuesta.Content.ReadAsStringAsync(ct);
        if (detalle.Length > 300) detalle = detalle[..300];
        throw new InvalidOperationException(
            $"Keycloak respondió {(int)respuesta.StatusCode} al {operacion}. {detalle}".TrimEnd());
    }

    public void Dispose()
    {
        _http.Dispose();
        _candado.Dispose();
    }
}
