using FlowRide.Application.Borde.Entrada.Dto;
using FlowRide.Application.Borde.Entrada.Puertos;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace FlowRide.Borde.Rutas;

/// <summary>
/// Tabla de ruteo vigente del borde como configuración de YARP: una ruta por operador activo, que solo acepta su host
/// (<c>{subdominio}.{dominio}</c>) y lleva a un único destino, su stack (OPLOG-SEG-02, ADM-SEG-02). Un host sin ruta
/// no llega a ningún stack (404). Se reemplaza entera al recargar.
/// </summary>
public sealed class ProveedorRutasBorde : IProxyConfigProvider
{
    /// <summary>Las rutas de los stacks van detrás de los endpoints propios del borde (webhooks, salud).</summary>
    public const int OrdenRutasStacks = 100;

    public const string PoliticaLimite = "panel";

    private volatile ConfiguracionProxy _actual;

    public ProveedorRutasBorde(BordeOptions opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        _actual = new ConfiguracionProxy(RutasVigentes.Vacias(opciones.DominioBase));
    }

    public RutasVigentes Vigentes => _actual.Rutas;

    public IProxyConfig GetConfig() => _actual;

    public void Publicar(RutasVigentes rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);
        var anterior = _actual;
        _actual = new ConfiguracionProxy(rutas);
        anterior.AvisarCambio();
    }

    private sealed class ConfiguracionProxy : IProxyConfig
    {
        private readonly CancellationTokenSource _cambio = new();

        public ConfiguracionProxy(RutasVigentes rutas)
        {
            Rutas = rutas;
            Routes = rutas.Rutas.Select(r => new RouteConfig
            {
                RouteId = $"operador-{r.TenantId}",
                ClusterId = $"stack-{r.TenantId}",
                Order = OrdenRutasStacks,
                RateLimiterPolicy = PoliticaLimite,
                Match = new RouteMatch { Hosts = [r.Host], Path = "/{**resto}" }
            }).ToList();
            Clusters = rutas.Rutas.Select(r => new ClusterConfig
            {
                ClusterId = $"stack-{r.TenantId}",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.Ordinal)
                {
                    ["stack"] = new() { Address = r.UrlStack.ToString() }
                }
            }).ToList();
            ChangeToken = new CancellationChangeToken(_cambio.Token);
        }

        public RutasVigentes Rutas { get; }
        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public IChangeToken ChangeToken { get; }

        public void AvisarCambio() => _cambio.Cancel();
    }
}

/// <summary>
/// Relee la tabla de ruteo del catálogo cada <see cref="BordeOptions.RecargaRutasSegundos"/> segundos, sin reiniciar
/// (ADR-0012). Si el catálogo no responde, el borde sigue con la última tabla buena.
/// </summary>
public sealed partial class RecargaRutasBorde(
    IServiceScopeFactory alcances,
    ProveedorRutasBorde proveedor,
    BordeOptions opciones,
    ILogger<RecargaRutasBorde> registro) : BackgroundService
{
    public async Task RecargarAsync(CancellationToken ct)
    {
        await using var alcance = alcances.CreateAsyncScope();
        var rutas = await alcance.ServiceProvider.GetRequiredService<IBordePuertoEntrada>().CargarRutasAsync(opciones.DominioBase, ct);
        proveedor.Publicar(rutas);
        RutasCargadas(registro, rutas.Rutas.Count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var espera = TimeSpan.FromSeconds(Math.Clamp(opciones.RecargaRutasSegundos, 5, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecargarAsync(stoppingToken);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                FalloRecarga(registro, error);
            }

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Tabla de ruteo recargada: {Rutas} operador(es).")]
    private static partial void RutasCargadas(ILogger registro, int rutas);

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudo recargar la tabla de ruteo; sigue la anterior.")]
    private static partial void FalloRecarga(ILogger registro, Exception error);
}
