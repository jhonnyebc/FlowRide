using System.Net;
using System.Text;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Domain.Canal.GestionCanal;
using FlowRide.Infrastructure.Canal;

namespace FlowRide.IntegrationTests.Canal;

/// <summary>Formato del webhook de Meta y adaptador del número de prueba, sin red (ADR-0008).</summary>
[Trait("Category", "Integration")]
public class AdaptadoresWhatsAppTests
{
    private static readonly WhatsAppOptions Opciones = new()
    {
        Proveedor = WhatsAppOptions.ProveedorMetaSandbox,
        PhoneNumberId = "106540352242922",
        AppSecret = "secreto",
        VerifyToken = "verificar",
        AccessToken = "token-de-acceso"
    };

    private const string Webhook = """
        {"object":"whatsapp_business_account","entry":[{"id":"WABA","changes":[
          {"field":"messages","value":{"messaging_product":"whatsapp",
            "metadata":{"display_phone_number":"15550000000","phone_number_id":"106540352242922"},
            "contacts":[{"profile":{"name":"Ana"},"wa_id":"573001112233"}],
            "messages":[
              {"from":"573001112233","id":"wamid.texto","timestamp":"1760000000","type":"text","text":{"body":"Hola ñandú"}},
              {"from":"573001112233","id":"wamid.boton","timestamp":"1760000001","type":"interactive","interactive":{"type":"button_reply","button_reply":{"id":"b1","title":"1"}}},
              {"from":"573001112233","id":"wamid.foto","timestamp":"1760000002","type":"image","image":{"id":"img"}}],
            "statuses":[{"id":"wamid.salida","status":"failed","timestamp":"1760000003","recipient_id":"573001112233",
              "errors":[{"code":131047,"title":"Re-engagement message"}]}]}},
          {"field":"messages","value":{"metadata":{"phone_number_id":"otro"},"messages":[{"from":"57","id":"wamid.ajeno","type":"text","text":{"body":"x"}}]}},
          {"field":"account_update","value":{}}]}]}
        """;

    [Fact]
    public void Firma_HmacSha256DelCuerpo_ConElSecretoDeLaApp()
    {
        var firma = FormatoWebhookMeta.Firmar("secreto", Webhook);

        Assert.StartsWith("sha256=", firma, StringComparison.Ordinal);
        Assert.True(FormatoWebhookMeta.FirmaValida(Opciones, Webhook, firma));
        Assert.True(FormatoWebhookMeta.FirmaValida(Opciones, Webhook, firma.ToUpperInvariant().Replace("SHA256=", "sha256=", StringComparison.Ordinal)));
        Assert.False(FormatoWebhookMeta.FirmaValida(Opciones, Webhook + " ", firma));
        Assert.False(FormatoWebhookMeta.FirmaValida(Opciones, Webhook, firma[7..]));
        Assert.False(FormatoWebhookMeta.FirmaValida(Opciones, Webhook, null));
        Assert.False(FormatoWebhookMeta.FirmaValida(new WhatsAppOptions(), Webhook, firma));
        Assert.True(FormatoWebhookMeta.VerificarSuscripcion(Opciones, "subscribe", "verificar"));
        Assert.False(FormatoWebhookMeta.VerificarSuscripcion(Opciones, "otro", "verificar"));
        Assert.False(FormatoWebhookMeta.VerificarSuscripcion(new WhatsAppOptions(), "subscribe", null));
    }

    [Fact]
    public void Traducir_NormalizaMensajesYEstados_EIgnoraOtrosNumeros()
    {
        var traducido = FormatoWebhookMeta.Traducir(Opciones, Webhook);

        Assert.Equal(
        [
            new MensajeRecibido("wamid.texto", "+573001112233", MensajeRecibido.TipoTexto, "Hola ñandú", DateTimeOffset.FromUnixTimeSeconds(1760000000)),
            new MensajeRecibido("wamid.boton", "+573001112233", MensajeRecibido.TipoTexto, "1", DateTimeOffset.FromUnixTimeSeconds(1760000001)),
            new MensajeRecibido("wamid.foto", "+573001112233", MensajeRecibido.TipoNoSoportado, "", DateTimeOffset.FromUnixTimeSeconds(1760000002))
        ], traducido.Mensajes);
        Assert.Equal(new EstadoEntrega("wamid.salida", "failed", "131047: Re-engagement message"), Assert.Single(traducido.Estados));
        Assert.Equal(2, traducido.Ignorados);
        Assert.Throws<FormatException>(() => FormatoWebhookMeta.Traducir(Opciones, "no es json"));
        Assert.Throws<FormatException>(() => FormatoWebhookMeta.Traducir(Opciones, "{\"entry\":3}"));
    }

    private sealed class ManejadorDePrueba(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Ultima { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Ultima = request;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo) =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Enviar_LlamaALaGraphApi_YDevuelveElIdDelMensaje()
    {
        var manejador = new ManejadorDePrueba(_ => Respuesta(HttpStatusCode.OK, """{"messaging_product":"whatsapp","messages":[{"id":"wamid.nuevo"}]}"""));
        var adaptador = new WhatsAppSandboxAdapter(Opciones, new HttpClient(manejador));

        var resultado = await adaptador.EnviarTextoAsync("+573001112233", "Hola", CancellationToken.None);

        Assert.Equal(ResultadoEnvio.Enviado("wamid.nuevo"), resultado);
        Assert.Equal("https://graph.facebook.com/v23.0/106540352242922/messages", manejador.Ultima!.RequestUri!.ToString());
        Assert.Equal(("Bearer", "token-de-acceso"), (manejador.Ultima.Headers.Authorization!.Scheme, manejador.Ultima.Headers.Authorization.Parameter));
        Assert.Contains("\"to\":\"573001112233\"", manejador.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("\"body\":\"Hola\"", manejador.Cuerpo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, TipoResultadoEnvio.ErrorTransitorio)]
    [InlineData(HttpStatusCode.ServiceUnavailable, TipoResultadoEnvio.ErrorTransitorio)]
    [InlineData(HttpStatusCode.RequestTimeout, TipoResultadoEnvio.ErrorTransitorio)]
    [InlineData(HttpStatusCode.BadRequest, TipoResultadoEnvio.ErrorPermanente)]
    [InlineData(HttpStatusCode.Unauthorized, TipoResultadoEnvio.ErrorPermanente)]
    public async Task Enviar_ClasificaLosErroresDeMeta(HttpStatusCode codigo, TipoResultadoEnvio esperado)
    {
        var adaptador = new WhatsAppSandboxAdapter(Opciones, new HttpClient(new ManejadorDePrueba(_ =>
            Respuesta(codigo, """{"error":{"message":"(#131030) Recipient phone number not in allowed list","code":131030}}"""))));

        var resultado = await adaptador.EnviarTextoAsync("+573001112233", "Hola", CancellationToken.None);

        Assert.Equal(esperado, resultado.Tipo);
        Assert.Contains("131030", resultado.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enviar_FallaDeRedOTimeout_EsTransitoria_YSinIdEsPermanente()
    {
        var red = new WhatsAppSandboxAdapter(Opciones, new HttpClient(new ManejadorDePrueba(_ => throw new HttpRequestException("sin red"))));
        var timeout = new WhatsAppSandboxAdapter(Opciones, new HttpClient(new ManejadorDePrueba(_ => throw new TaskCanceledException())));
        var sinId = new WhatsAppSandboxAdapter(Opciones, new HttpClient(new ManejadorDePrueba(_ => Respuesta(HttpStatusCode.OK, "{}"))));

        Assert.Equal(TipoResultadoEnvio.ErrorTransitorio, (await red.EnviarTextoAsync("+57", "x", CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultadoEnvio.ErrorTransitorio, (await timeout.EnviarTextoAsync("+57", "x", CancellationToken.None)).Tipo);
        Assert.Equal(TipoResultadoEnvio.ErrorPermanente, (await sinId.EnviarTextoAsync("+57", "x", CancellationToken.None)).Tipo);
    }

    [Fact]
    public async Task Simulado_GuardaLoEnviado_YPuedeFallarAPedido()
    {
        var simulado = new WhatsAppSimuladoAdapter(Opciones);
        simulado.FallarProximos.Enqueue(ResultadoEnvio.Permanente("HTTP 400"));

        Assert.Equal(TipoResultadoEnvio.ErrorPermanente, (await simulado.EnviarTextoAsync("+57", "uno", CancellationToken.None)).Tipo);
        Assert.Equal(ResultadoEnvio.Enviado("wamid.simulado.1"), await simulado.EnviarTextoAsync("+57", "dos", CancellationToken.None));
        Assert.Equal(("+57", "dos"), (Assert.Single(simulado.Enviados).Telefono, simulado.Enviados.Single().Texto));
        Assert.True(simulado.FirmaValida("{}", FormatoWebhookMeta.Firmar("secreto", "{}")));
        Assert.Equal(3, simulado.Traducir(Webhook).Mensajes.Count);
    }
}
