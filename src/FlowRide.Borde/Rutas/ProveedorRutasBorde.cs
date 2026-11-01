using FlowRide.Application.Borde.Entrada.Dto;
using FlowRide.Application.Borde.Entrada.Puertos;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace FlowRide.Borde.Rutas;

/// <summary>
/// Tabla de ruteo vigente del borde como configuración de YARP: por cada operador activo, rutas que solo aceptan su host
/// (<c>{subdominio}.{dominio}</c>) y llevan a un único destino, su stack (OPLOG-SEG-02, ADM-SEG-02). Un host sin ruta
/// no llega a ningún stack (404). Se reemplaza entera al recargar.
/// </summary>
public sealed class ProveedorRutasBorde : IProxyConfigProvider, IDisposable
{
    /// <summary>Las rutas de los stacks van detrás de los endpoints propios del borde (webhooks, salud).</summary>
    public const int OrdenRutasStacks = 100;

    public const string PoliticaLimite = "panel";

    /// <summary>
    /// Lo único del stack que el borde expone: su API y su salud. El resto del subdominio es el Panel Web, que sirve el
    /// propio borde (<see cref="PanelWebBorde"/>); así nada más del stack queda alcanzable desde afuera.
    /// </summary>
    private static readonly (string Nombre, string Patron)[] RutasDelStack = [("api", "/api/{**resto}"), ("salud", "/health/{**resto}")];

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
        anterior.Dispose();
    }

    public void Dispose() => _actual.Dispose();

    /// <remarks>
    /// Al reemplazarla se cancela su token (YARP relee la configuración) y se libera. Un token ya cancelado sigue
    /// respondiendo como cambiado aunque su origen esté liberado.
    /// </remarks>
    private sealed class ConfiguracionProxy : IProxyConfig, IDisposable
    {
        private readonly CancellationTokenSource _cambio = new();

        public ConfiguracionProxy(RutasVigentes rutas)
        {
            Rutas = rutas;
            Routes = rutas.Rutas.SelectMany(r => RutasDelStack.Select(ruta => new RouteConfig
            {
                RouteId = $"operador-{r.TenantId}-{ruta.Nombre}",
                ClusterId = $"stack-{r.TenantId}",
                Order = OrdenRutasStacks,
                RateLimiterPolicy = PoliticaLimite,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal) { [Seguridad.TokensBorde.MetadatoOperador] = r.TenantId },
                Match = new RouteMatch { Hosts = [r.Host], Path = ruta.Patron }
            })).ToList();
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

        public void Dispose() => _cambio.Dispose();
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
