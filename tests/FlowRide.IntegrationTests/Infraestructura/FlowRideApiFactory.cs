using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>Levanta la API real del stack de un operador con la configuración indicada.</summary>
public sealed class FlowRideApiFactory(string? cadenaConexion, string? tenantId = "tenant-a")
    : WebApplicationFactory<Program>
{
    public RegistroCapturado Registro { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (cadenaConexion is not null) builder.UseSetting("ConnectionStrings:Silo", cadenaConexion);
        if (tenantId is not null) builder.UseSetting("FlowRide:TenantId", tenantId);

        builder.ConfigureLogging(logging => logging.AddProvider(Registro));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RutaDeErrorDePrueba>());
    }
}
