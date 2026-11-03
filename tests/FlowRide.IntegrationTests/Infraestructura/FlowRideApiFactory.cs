using FlowRide.Application.Identidad.Salida.Puertos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Levanta la API real del stack de un operador con la configuración indicada. Por defecto apunta a un Keycloak
/// inexistente: las pruebas que necesitan autenticarse usan <see cref="TokensDePrueba"/> o un Keycloak real.
/// </summary>
public sealed class FlowRideApiFactory(string? cadenaConexion, string? tenantId = "tenant-a")
    : WebApplicationFactory<Program>
{
    public RegistroCapturado Registro { get; } = new();

    public string? UrlKeycloak { get; init; } = "http://keycloak.pruebas.invalid";
    public string? SecretoCliente { get; init; } = "secreto-de-prueba";

    /// <summary>Acepta los tokens firmados por <see cref="TokensDePrueba"/> en lugar de los del realm.</summary>
    public bool ConTokensDePrueba { get; init; }

    /// <summary>Si se indica, reemplaza al adaptador de Keycloak.</summary>
    public IProveedorIdentidad? Proveedor { get; init; }

    /// <summary>Secretos del operador (p. ej. <see cref="EntornoAprovisionamiento.AjustesDelStackAsync"/>); pisan los de prueba.</summary>
    public IReadOnlyDictionary<string, string?> Ajustes { get; init; } = new Dictionary<string, string?>();

    /// <summary>Clave de cifrado de campos para las pruebas que no usan un operador aprovisionado.</summary>
    public const string ClaveCifradoDePrueba = "clave-de-cifrado-de-pruebas-de-flowride-0001";

    /// <summary>Canal WhatsApp de prueba: proveedor simulado con el formato y la firma de Meta.</summary>
    public const string SecretoAppWhatsApp = "secreto-de-la-app-de-meta-de-pruebas";

    public const string TokenVerificacionWhatsApp = "token-de-verificacion-de-pruebas";
    public const string NumeroWhatsApp = "1234567890";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (cadenaConexion is not null) builder.UseSetting("ConnectionStrings:Silo", cadenaConexion);
        if (tenantId is not null) builder.UseSetting("FlowRide:TenantId", tenantId);
        if (UrlKeycloak is not null) builder.UseSetting("FlowRide:Identidad:UrlKeycloak", UrlKeycloak);
        if (SecretoCliente is not null) builder.UseSetting("FlowRide:Identidad:SecretoCliente", SecretoCliente);
        builder.UseSetting("FlowRide:Identidad:RequiereHttps", "false");
        builder.UseSetting("FlowRide:Cifrado:Claves:v1", ClaveCifradoDePrueba);
        builder.UseSetting("FlowRide:Cifrado:Vigente", "v1");
        builder.UseSetting("ConnectionStrings:Objetos", "UseDevelopmentStorage=true");
        // Los workers de la cola y del canal se apagan en las pruebas: cada prueba los corre cuando lo necesita.
        builder.UseSetting("FlowRide:Asignacion:Worker:Habilitado", "false");
        builder.UseSetting("FlowRide:WhatsApp:Worker:Habilitado", "false");
        builder.UseSetting("FlowRide:Notificaciones:Worker:Habilitado", "false");
        // Sin servidor de correo real: las pruebas de correo usan su propio Mailpit (Ajustes).
        builder.UseSetting("FlowRide:Correo:Host", "smtp.pruebas.invalid");
        builder.UseSetting("FlowRide:WhatsApp:Proveedor", "Simulado");
        builder.UseSetting("FlowRide:WhatsApp:PhoneNumberId", NumeroWhatsApp);
        builder.UseSetting("FlowRide:WhatsApp:AppSecret", SecretoAppWhatsApp);
        builder.UseSetting("FlowRide:WhatsApp:VerifyToken", TokenVerificacionWhatsApp);
        foreach (var (clave, valor) in Ajustes)
            if (valor is not null) builder.UseSetting(clave, valor);

        builder.ConfigureLogging(logging => logging.AddProvider(Registro));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RutaDeErrorDePrueba>());
        builder.ConfigureTestServices(services =>
        {
            if (ConTokensDePrueba) TokensDePrueba.Usar(services);
            if (Proveedor is not null) services.Replace(ServiceDescriptor.Singleton(Proveedor));
        });
    }
}
