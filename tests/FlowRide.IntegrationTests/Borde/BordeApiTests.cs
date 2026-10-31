using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Borde.Rutas;
using FlowRide.Infrastructure.Canal;
using FlowRide.IntegrationTests.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.IntegrationTests.Borde;

/// <summary>Borde común (ADR-0012) de punta a punta: borde real, catálogo real, API real del stack y su Silo.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class BordeApiTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    private string HostOperador => $"{Tenant}.{BordeFactory.DominioBase}";

    private Task<RutaOperadorRespuesta> RegistrarRutaAsync(string url = "http://stack.pruebas", string? numero = FlowRideApiFactory.NumeroWhatsApp) =>
        Entorno.UsarAsync(servicios => servicios.GetRequiredService<IRutasOperadorPuertoEntrada>().RegistrarAsync(
            new RegistrarRutaOperadorPeticion { TenantId = Tenant, UrlStack = url, PhoneNumberId = numero }, CancellationToken.None));

    private async Task<BordeFactory> BordeAsync(int limitePanel = 1000)
    {
        var borde = new BordeFactory(Entorno.CadenaCatalogo, () => Api.Server.CreateHandler()) { PeticionesPorMinutoPanel = limitePanel };
        await RecargarAsync(borde);
        return borde;
    }

    private static Task RecargarAsync(BordeFactory borde) => borde.Services.GetRequiredService<RecargaRutasBorde>().RecargarAsync(CancellationToken.None);

    private HttpClient Cliente(BordeFactory borde, string? host = null, bool conToken = false)
    {
        var cliente = borde.CreateClient();
        cliente.DefaultRequestHeaders.Host = host ?? HostOperador;
        if (conToken) cliente.DefaultRequestHeaders.Authorization = Operador.DefaultRequestHeaders.Authorization;
        return cliente;
    }

    private static string Webhook(string numero) => JsonSerializer.Serialize(new
    {
        @object = "whatsapp_business_account",
        entry = new[]
        {
            new
            {
                id = "WABA",
                changes = new[]
                {
                    new
                    {
                        field = "messages",
                        value = new
                        {
                            messaging_product = "whatsapp",
                            metadata = new { display_phone_number = "15550000000", phone_number_id = numero },
                            messages = new[]
                            {
                                new
                                {
                                    from = "573001112233", id = $"wamid.{Guid.NewGuid():N}",
                                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), type = "text",
                                    text = new { body = "hola" }
                                }
                            }
                        }
                    }
                }
            }
        }
    });

    private static async Task<HttpResponseMessage> EnviarWebhookAsync(HttpClient cliente, string cuerpo, string? firma = null)
    {
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", firma ?? FormatoWebhookMeta.Firmar(FlowRideApiFactory.SecretoAppWhatsApp, cuerpo));
        return await cliente.PostAsync("/api/whatsapp/webhook", contenido);
    }

    private Task<long> EntrantesAsync() => EjecutarEnSiloAsync<long>("SELECT count(*) FROM canal.mensaje_entrante");

    [Fact]
    public async Task ElBorde_SoloReenviaALaApiConUnTokenDelOperadorDeEseSubdominio()
    {
        await RegistrarRutaAsync();
        await using var borde = await BordeAsync();
        HttpClient ConToken(string token)
        {
            var cliente = Cliente(borde);
            cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return cliente;
        }

        var sinToken = await Cliente(borde).GetAsync("/api/viajes");
        var basura = await ConToken("no-es-un-jwt").GetAsync("/api/viajes");
        var deOtroOperador = await ConToken(TokensDePrueba.Crear("sub-ajeno", "otro-operador")).GetAsync("/api/viajes");
        var vencido = await ConToken(TokensDePrueba.Crear("sub-x", Tenant, expira: DateTime.UtcNow.AddMinutes(-5))).GetAsync("/api/viajes");
        Assert.Equal(0, borde.Reenvios);

        var valido = await Cliente(borde, conToken: true).GetAsync("/api/viajes");
        var fueraDeLaApi = await Cliente(borde).GetAsync("/no-es-api");

        Assert.All(new[] { sinToken, basura, deOtroOperador, vencido }, r =>
        {
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal("Bearer", r.Headers.WwwAuthenticate.ToString());
        });
        Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
        // Del stack solo se exponen /api y /health: lo demás ni siquiera le llega.
        Assert.Equal(HttpStatusCode.NotFound, fueraDeLaApi.StatusCode);
        Assert.Equal(1, borde.Reenvios);
    }

    [Fact]
    public async Task ElBorde_SirveElPanelWeb_YDejaSoloLaApiYLaSaludAlStack()
    {
        await RegistrarRutaAsync();
        var carpeta = Directory.CreateTempSubdirectory("panel-web-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(carpeta, "index.html"), "<!doctype html><title>Panel FlowRide</title>");
            Directory.CreateDirectory(Path.Combine(carpeta, "assets"));
            await File.WriteAllTextAsync(Path.Combine(carpeta, "assets", "app-3f9c2ab.js"), "console.log('panel');");
            await using var borde = new BordeFactory(Entorno.CadenaCatalogo, () => Api.Server.CreateHandler()) { PanelWeb = carpeta };
            await RecargarAsync(borde);

            var inicio = await Cliente(borde).GetAsync("/");
            var rutaDelPanel = await Cliente(borde).GetAsync("/operacion/solicitudes");
            var recurso = await Cliente(borde).GetAsync("/assets/app-3f9c2ab.js");
            var apiSinToken = await Cliente(borde).GetAsync("/api/viajes");
            var apiDeHostDesconocido = await Cliente(borde, $"desconocido.{BordeFactory.DominioBase}").GetAsync("/api/viajes");
            var postAlPanel = await Cliente(borde).PostAsync("/operacion/solicitudes", new StringContent(""));
            Assert.Equal(0, borde.Reenvios);
            var saludDelStack = await Cliente(borde).GetAsync("/health/live");

            foreach (var pagina in new[] { inicio, rutaDelPanel })
            {
                Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
                Assert.Equal("text/html", pagina.Content.Headers.ContentType?.MediaType);
                Assert.Contains("<title>Panel FlowRide</title>", await pagina.Content.ReadAsStringAsync(), StringComparison.Ordinal);
                Assert.Contains("frame-ancestors 'none'", pagina.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
            }
            Assert.Equal(HttpStatusCode.OK, recurso.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, apiSinToken.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, apiDeHostDesconocido.StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, postAlPanel.StatusCode);
            Assert.Equal(HttpStatusCode.OK, saludDelStack.StatusCode);
            Assert.Equal(1, borde.Reenvios);
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact]
    public async Task Proxy_LlevaCadaSubdominioSoloASuStack_ConCabecerasDeSeguridad()
    {
        await RegistrarRutaAsync();
        await using var borde = await BordeAsync();

        var sinToken = await Cliente(borde).GetAsync("/api/viajes");
        var conToken = await Cliente(borde, conToken: true).GetAsync("/api/viajes");
        var otroHost = await Cliente(borde, $"otro.{BordeFactory.DominioBase}", conToken: true).GetAsync("/api/viajes");
        var sinSubdominio = await Cliente(borde, "localhost").GetAsync("/api/viajes");
        var trace = await Cliente(borde).SendAsync(new HttpRequestMessage(HttpMethod.Trace, "/api/viajes"));
        var salud = await Cliente(borde, "localhost").GetAsync("/salud");

        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, conToken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otroHost.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sinSubdominio.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, trace.StatusCode);
        Assert.Equal(HttpStatusCode.OK, salud.StatusCode);
        foreach (var respuesta in new[] { sinToken, conToken, otroHost })
        {
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", respuesta.Headers.GetValues("X-Frame-Options").Single());
            Assert.Contains("frame-ancestors 'none'", respuesta.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
            Assert.False(respuesta.Headers.Contains("Server"));
        }
    }

    [Fact]
    public async Task Webhook_LlegaAlStackDeSuNumero_ConFirmaValidada()
    {
        await RegistrarRutaAsync();
        await using var borde = await BordeAsync();
        var meta = Cliente(borde, "meta.ngrok.pruebas");

        var valido = await EnviarWebhookAsync(meta, Webhook(FlowRideApiFactory.NumeroWhatsApp));
        var conFirmaMala = await EnviarWebhookAsync(meta, Webhook(FlowRideApiFactory.NumeroWhatsApp), "sha256=" + new string('0', 64));
        var deOtroNumero = await EnviarWebhookAsync(meta, Webhook("999999999"));
        var roto = await EnviarWebhookAsync(meta, "{\"entry\":5}");
        var handshake = await meta.GetStringAsync($"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={BordeFactory.TokenVerificacion}&hub.challenge=reto-42");
        var handshakeMalo = await meta.GetAsync("/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=otro&hub.challenge=reto-42");

        Assert.Equal(HttpStatusCode.OK, valido.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, conFirmaMala.StatusCode);
        Assert.Equal(HttpStatusCode.OK, deOtroNumero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, roto.StatusCode);
        Assert.Equal("reto-42", handshake);
        Assert.Equal(HttpStatusCode.Forbidden, handshakeMalo.StatusCode);
        Assert.Equal(1L, await EntrantesAsync());
    }

    [Fact]
    public async Task UnaRutaNueva_SeTomaAlRecargar_SinReiniciarElBorde()
    {
        await using var borde = await BordeAsync();
        var antes = await Cliente(borde).GetAsync("/api/viajes");

        await RegistrarRutaAsync();
        await RecargarAsync(borde);
        var despues = await Cliente(borde).GetAsync("/api/viajes");

        Assert.Equal(HttpStatusCode.NotFound, antes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task ElLimiteDeTasa_Responde429ConRetryAfter()
    {
        await RegistrarRutaAsync();
        await using var borde = await BordeAsync(limitePanel: 2);
        var cliente = Cliente(borde);

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 3; i++) respuestas.Add(await cliente.GetAsync("/api/viajes"));

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests],
            respuestas.Select(r => r.StatusCode));
        Assert.Equal("60", respuestas[2].Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task SiElStackNoResponde_ElWebhookDevuelve502ParaQueMetaReintente()
    {
        await RegistrarRutaAsync($"http://{BordeFactory.HostCaido}");
        await using var borde = await BordeAsync();

        var respuesta = await EnviarWebhookAsync(Cliente(borde, "meta.ngrok.pruebas"), Webhook(FlowRideApiFactory.NumeroWhatsApp));

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        Assert.Equal(0L, await EntrantesAsync());
    }
}
