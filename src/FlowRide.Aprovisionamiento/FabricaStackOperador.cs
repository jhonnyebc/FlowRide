using FlowRide.Infrastructure;
using FlowRide.Infrastructure.Identidad;
using FlowRide.Infrastructure.Plataforma;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.Aprovisionamiento;

/// <summary>
/// Arma, dentro de la herramienta, la misma composición que el stack de un operador (su Silo y su realm, con sus
/// propios secretos). Así el primer administrador se crea con el mismo caso de uso que usa la API, y con las
/// credenciales del operador, no con las de la plataforma.
/// </summary>
public sealed class FabricaStackOperador(IConfiguration configuracionPlataforma, string? raizSecretos = null)
{
    public ServiceProvider Crear(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        // Solo la identidad del operador y la configuración no secreta de Keycloak; sus secretos, de su almacén.
        var valores = new Dictionary<string, string?> { [$"{SiloOptions.Seccion}:TenantId"] = tenantId };
        foreach (var clave in new[] { "UrlKeycloak", "UrlPanel" })
        {
            var valor = configuracionPlataforma[$"{IdentidadOptions.Seccion}:{clave}"];
            if (valor is not null) valores[$"{IdentidadOptions.Seccion}:{clave}"] = valor;
        }

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(valores)
            .AddSecretosLocalesDelOperador(tenantId, raizSecretos)
            .Build();

        return new ServiceCollection()
            .AddInfraestructura(configuracion)
            .AddIdentidad(configuracion)
            .BuildServiceProvider();
    }
}
