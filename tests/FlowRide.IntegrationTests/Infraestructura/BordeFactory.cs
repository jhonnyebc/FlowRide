using FlowRide.Borde;
using FlowRide.Borde.Seguridad;
using FlowRide.Borde.Webhooks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Yarp.ReverseProxy.Forwarder;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Levanta el borde común real contra un catálogo real. Lo que reenvía (proxy y webhooks) va al stack en memoria que se
/// indique; un stack con host <see cref="HostCaido"/> simula uno que no responde.
/// </summary>
public sealed class BordeFactory(string cadenaCatalogo, Func<HttpMessageHandler> haciaElStack) : WebApplicationFactory<MarcadorBorde>
{
    public const string HostCaido = "caido.invalid";
    public const string TokenVerificacion = "token-de-verificacion-del-borde";
    public const string DominioBase = "dev.localhost";

    public int PeticionesPorMinutoPanel { get; init; } = 1000;

    private int _reenvios;

    /// <summary>Peticiones que el borde dejó pasar hacia algún stack (proxy o webhook).</summary>
    public int Reenvios => Volatile.Read(ref _reenvios);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Catalogo", cadenaCatalogo);
        builder.UseSetting("Borde:WhatsApp:AppSecret", FlowRideApiFactory.SecretoAppWhatsApp);
        builder.UseSetting("Borde:WhatsApp:VerifyToken", TokenVerificacion);
        builder.UseSetting("Borde:DominioBase", DominioBase);
        builder.UseSetting("Borde:RecargaRutasSegundos", "3600");
        builder.UseSetting("Borde:PeticionesPorMinutoPanel", PeticionesPorMinutoPanel.ToString(System.Globalization.CultureInfo.InvariantCulture));

        builder.ConfigureTestServices(servicios =>
        {
            servicios.Replace(ServiceDescriptor.Singleton<IForwarderHttpClientFactory>(new ReenvioDePrueba(() => Handler())));
            servicios.AddHttpClient(WebhookEndpoints.ClienteStacks).ConfigurePrimaryHttpMessageHandler(Handler);
            servicios.Replace(ServiceDescriptor.Singleton<IEmisoresTokens>(new EmisoresDePrueba()));
        });
    }

    private HttpMessageHandler Handler() => new StackDePrueba(haciaElStack(), () => Interlocked.Increment(ref _reenvios));

    /// <summary>Todos los operadores firman con el emisor de prueba de <see cref="TokensDePrueba"/>.</summary>
    private sealed class EmisoresDePrueba : IEmisoresTokens
    {
        public (string Emisor, IConfigurationManager<OpenIdConnectConfiguration> Claves) DelOperador(string tenantId) =>
            (TokensDePrueba.Emisor, TokensDePrueba.Configuracion());
    }

    private sealed class ReenvioDePrueba(Func<HttpMessageHandler> handler) : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(handler(), disposeHandler: true);
    }

    private sealed class StackDePrueba(HttpMessageHandler interno, Action alReenviar) : DelegatingHandler(interno)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.Host == HostCaido)
                throw new HttpRequestException("El stack no responde.");
            alReenviar();
            return base.SendAsync(request, cancellationToken);
        }
    }
}
