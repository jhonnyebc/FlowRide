using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.GestionCanal;

namespace FlowRide.Infrastructure.Canal;

/// <summary>
/// Configuración del canal WhatsApp del stack (ADR-0008). Lo no secreto va en appsettings; <c>AppSecret</c>,
/// <c>VerifyToken</c> y <c>AccessToken</c> son secretos del operador (almacén local en desarrollo, Key Vault en Fase 12).
/// </summary>
public sealed class WhatsAppOptions
{
    public const string Seccion = "FlowRide:WhatsApp";
    public const string ProveedorSimulado = "Simulado";
    public const string ProveedorMetaSandbox = "MetaSandbox";

    /// <summary><c>Simulado</c> (por defecto, sin red) o <c>MetaSandbox</c> (número de prueba de Meta Cloud API).</summary>
    public string Proveedor { get; set; } = ProveedorSimulado;

    /// <summary>Identificador del número de WhatsApp del operador en Meta (no es secreto).</summary>
    public string? PhoneNumberId { get; set; }

    public string UrlGraph { get; set; } = "https://graph.facebook.com";
    public string VersionGraph { get; set; } = "v23.0";

    /// <summary>Secreto de la app de Meta: firma <c>X-Hub-Signature-256</c> de cada webhook.</summary>
    public string? AppSecret { get; set; }

    /// <summary>Token que Meta envía al verificar la suscripción del webhook.</summary>
    public string? VerifyToken { get; set; }

    /// <summary>Token de acceso a la Graph API (usuario del sistema), solo para <c>MetaSandbox</c>.</summary>
    public string? AccessToken { get; set; }

    public bool EsSimulado => string.Equals(Proveedor, ProveedorSimulado, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Formato del webhook de Meta Cloud API: firma HMAC-SHA256 del cuerpo con el secreto de la app y payload
/// <c>entry[].changes[].value</c> con <c>messages</c> y <c>statuses</c>. Lo comparten el adaptador del número de prueba y el
/// simulado (que recibe webhooks con el mismo formato).
/// </summary>
public static class FormatoWebhookMeta
{
    private const string PrefijoFirma = "sha256=";

    public static bool VerificarSuscripcion(WhatsAppOptions opciones, string? modo, string? token) =>
        modo == "subscribe" && !string.IsNullOrEmpty(opciones.VerifyToken) && token is not null
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(opciones.VerifyToken));

    public static string Firmar(string secreto, string cuerpo) =>
        PrefijoFirma + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(cuerpo)));

    public static bool FirmaValida(WhatsAppOptions opciones, string cuerpo, string? firma)
    {
        if (string.IsNullOrEmpty(opciones.AppSecret) || firma is null || !firma.StartsWith(PrefijoFirma, StringComparison.Ordinal))
            return false;
        var esperada = Encoding.ASCII.GetBytes(Firmar(opciones.AppSecret, cuerpo));
        return CryptographicOperations.FixedTimeEquals(esperada, Encoding.ASCII.GetBytes(firma.ToLowerInvariant()));
    }

    public static WebhookTraducido Traducir(WhatsAppOptions opciones, string cuerpo)
    {
        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(cuerpo);
        }
        catch (JsonException error)
        {
            throw new FormatException("El webhook no es un JSON válido.", error);
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("entry", out var entradas) || entradas.ValueKind != JsonValueKind.Array)
                throw new FormatException("El webhook no tiene el formato de WhatsApp Cloud API.");

            var mensajes = new List<MensajeRecibido>();
            var estados = new List<EstadoEntrega>();
            var ignorados = 0;
            foreach (var cambio in entradas.EnumerateArray().SelectMany(e => Arreglo(e, "changes")))
            {
                if (Texto(cambio, "field") != "messages" || !cambio.TryGetProperty("value", out var valor))
                {
                    ignorados++;
                    continue;
                }

                // El stack atiende solo el número de su operador (ADR-0002; el ruteo por número llega con el borde, Fase 10).
                var numero = valor.TryGetProperty("metadata", out var metadatos) ? Texto(metadatos, "phone_number_id") : null;
                if (!string.IsNullOrEmpty(opciones.PhoneNumberId) && numero != opciones.PhoneNumberId)
                {
                    ignorados++;
                    continue;
                }

                mensajes.AddRange(Arreglo(valor, "messages").Select(Mensaje));
                estados.AddRange(Arreglo(valor, "statuses").Select(Estado));
            }
            return new WebhookTraducido(mensajes, estados, ignorados);
        }
    }

    private static MensajeRecibido Mensaje(JsonElement m)
    {
        var id = Texto(m, "id") ?? throw new FormatException("Mensaje sin id.");
        var telefono = EntradaConversacion.TelefonoE164(Texto(m, "from") ?? throw new FormatException("Mensaje sin remitente."));
        var recibido = long.TryParse(Texto(m, "timestamp"), NumberStyles.None, CultureInfo.InvariantCulture, out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos)
            : DateTimeOffset.UtcNow;

        var contenido = Texto(m, "type") switch
        {
            "text" => m.TryGetProperty("text", out var t) ? Texto(t, "body") : null,
            "button" => m.TryGetProperty("button", out var b) ? Texto(b, "text") : null,
            "interactive" => m.TryGetProperty("interactive", out var i)
                ? (i.TryGetProperty("button_reply", out var br) ? Texto(br, "title") : i.TryGetProperty("list_reply", out var lr) ? Texto(lr, "title") : null)
                : null,
            _ => null
        };
        return contenido is null
            ? new MensajeRecibido(id, telefono, MensajeRecibido.TipoNoSoportado, string.Empty, recibido)
            : new MensajeRecibido(id, telefono, MensajeRecibido.TipoTexto, contenido, recibido);
    }

    private static EstadoEntrega Estado(JsonElement s)
    {
        var error = Arreglo(s, "errors").Select(e => $"{(e.TryGetProperty("code", out var c) ? c.ToString() : "?")}: {Texto(e, "title")}")
            .FirstOrDefault();
        return new EstadoEntrega(Texto(s, "id") ?? throw new FormatException("Estado sin id."), Texto(s, "status") ?? "desconocido", error);
    }

    private static IEnumerable<JsonElement> Arreglo(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var arreglo) && arreglo.ValueKind == JsonValueKind.Array
            ? arreglo.EnumerateArray().ToList()
            : [];

    private static string? Texto(JsonElement elemento, string propiedad) =>
        elemento.ValueKind == JsonValueKind.Object && elemento.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}

/// <summary>
/// Número de prueba de Meta Cloud API (ADR-0008, revisión 2026-10-13): mismo webhook y misma firma que producción. Envía
/// texto por la Graph API y clasifica los errores: 408/429/5xx y fallas de red son transitorios; el resto, permanentes.
/// </summary>
public sealed class WhatsAppSandboxAdapter(WhatsAppOptions opciones, HttpClient http) : IWhatsAppGateway
{
    public bool VerificarSuscripcion(string? modo, string? token) => FormatoWebhookMeta.VerificarSuscripcion(opciones, modo, token);

    public bool FirmaValida(string cuerpo, string? firma) => FormatoWebhookMeta.FirmaValida(opciones, cuerpo, firma);

    public WebhookTraducido Traducir(string cuerpo) => FormatoWebhookMeta.Traducir(opciones, cuerpo);

    public async Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Post,
            $"{opciones.UrlGraph.TrimEnd('/')}/{opciones.VersionGraph}/{opciones.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = telefono.TrimStart('+'),
                type = "text",
                text = new { preview_url = false, body = texto }
            })
        };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.AccessToken);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(peticion, ct);
        }
        catch (HttpRequestException error)
        {
            return ResultadoEnvio.Transitorio($"Red: {error.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoEnvio.Transitorio("Tiempo de espera agotado con la Graph API.");
        }

        using (respuesta)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.IsSuccessStatusCode)
                return IdMensaje(cuerpo) is { } id ? ResultadoEnvio.Enviado(id) : ResultadoEnvio.Permanente("La Graph API no devolvió el id del mensaje.");

            var error = $"HTTP {(int)respuesta.StatusCode}: {ErrorMeta(cuerpo)}";
            return respuesta.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)respuesta.StatusCode >= 500
                ? ResultadoEnvio.Transitorio(error)
                : ResultadoEnvio.Permanente(error);
        }
    }

    private static string? IdMensaje(string cuerpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            return documento.RootElement.TryGetProperty("messages", out var mensajes) && mensajes.ValueKind == JsonValueKind.Array
                   && mensajes.GetArrayLength() > 0 && mensajes[0].TryGetProperty("id", out var id)
                ? id.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ErrorMeta(string cuerpo)
    {
        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            return documento.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var mensaje)
                ? $"{(error.TryGetProperty("code", out var codigo) ? codigo.ToString() : "?")} {mensaje.GetString()}"
                : cuerpo.Length > 200 ? cuerpo[..200] : cuerpo;
        }
        catch (JsonException)
        {
            return cuerpo.Length > 200 ? cuerpo[..200] : cuerpo;
        }
    }
}

/// <summary>
/// Proveedor simulado en memoria (ADR-0008) para las pruebas y el desarrollo sin red: recibe webhooks con el formato de
/// Meta y "envía" guardando los mensajes. <see cref="FallarProximos"/> simula errores del proveedor.
/// </summary>
public sealed class WhatsAppSimuladoAdapter(WhatsAppOptions opciones) : IWhatsAppGateway
{
    private int _secuencia;

    public ConcurrentQueue<(string Telefono, string Texto, string IdExterno)> Enviados { get; } = new();

    /// <summary>Errores a devolver en los próximos envíos, en orden (transitorio o permanente).</summary>
    public ConcurrentQueue<ResultadoEnvio> FallarProximos { get; } = new();

    public bool VerificarSuscripcion(string? modo, string? token) => FormatoWebhookMeta.VerificarSuscripcion(opciones, modo, token);

    public bool FirmaValida(string cuerpo, string? firma) => FormatoWebhookMeta.FirmaValida(opciones, cuerpo, firma);

    public WebhookTraducido Traducir(string cuerpo) => FormatoWebhookMeta.Traducir(opciones, cuerpo);

    public Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct)
    {
        if (FallarProximos.TryDequeue(out var fallo))
            return Task.FromResult(fallo);

        var id = $"wamid.simulado.{Interlocked.Increment(ref _secuencia)}";
        Enviados.Enqueue((telefono, texto, id));
        return Task.FromResult(ResultadoEnvio.Enviado(id));
    }
}
