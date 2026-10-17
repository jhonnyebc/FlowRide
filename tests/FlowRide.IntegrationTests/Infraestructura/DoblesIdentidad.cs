using System.Collections.Concurrent;
using FlowRide.Application.Identidad.Salida.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>Proveedor OIDC en memoria para las pruebas de la API que no necesitan un Keycloak real.</summary>
public sealed class ProveedorIdentidadDePrueba : IProveedorIdentidad
{
    public ConcurrentDictionary<string, bool> Cuentas { get; } = new(StringComparer.Ordinal);
    public ConcurrentQueue<string> CorreosEnviados { get; } = new();

    public Task<string> AsegurarUsuarioAsync(string correo, string nombre, CancellationToken ct)
    {
        var id = $"sub-{correo}";
        Cuentas.TryAdd(id, true);
        return Task.FromResult(id);
    }

    public Task EnviarConfiguracionContrasenaAsync(string identidadExternaId, CancellationToken ct)
    {
        CorreosEnviados.Enqueue(identidadExternaId);
        return Task.CompletedTask;
    }

    public Task HabilitarAsync(string identidadExternaId, CancellationToken ct)
    {
        Cuentas[identidadExternaId] = true;
        return Task.CompletedTask;
    }

    public Task DeshabilitarAsync(string identidadExternaId, CancellationToken ct)
    {
        Cuentas[identidadExternaId] = false;
        return Task.CompletedTask;
    }
}

/// <summary>Servidor de identidad en memoria para las pruebas de aprovisionamiento que no necesitan Keycloak.</summary>
public sealed class ServidorIdentidadDePrueba : IServidorIdentidad
{
    public ConcurrentDictionary<string, string> Realms { get; } = new(StringComparer.Ordinal);

    public Task AsegurarRealmAsync(string nombreRealm, string tenantId, string secretoClienteServicio, CancellationToken ct)
    {
        Realms[nombreRealm] = tenantId;
        return Task.CompletedTask;
    }
}
