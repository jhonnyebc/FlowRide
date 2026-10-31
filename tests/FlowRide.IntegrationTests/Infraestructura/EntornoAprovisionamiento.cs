using System.Collections.Concurrent;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Aprovisionamiento;
using FlowRide.Infrastructure;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Seguridad;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Aprovisiona Silos reales en el PostgreSQL de la prueba con la misma composición que la herramienta
/// (<see cref="DependencyInjection.AddAprovisionamiento"/>). Cada entorno usa su propio catálogo y su propia
/// carpeta temporal de secretos, para que las pruebas no se pisen entre sí.
/// </summary>
public sealed class EntornoAprovisionamiento : IAsyncDisposable
{
    private readonly ServiceProvider _servicios;
    private readonly ConcurrentDictionary<string, CifradorCampos> _cifradores = new(StringComparer.Ordinal);

    private EntornoAprovisionamiento(ServiceProvider servicios, string raizSecretos, string cadenaAdministracion, string cadenaCatalogo)
    {
        _servicios = servicios;
        RaizSecretos = raizSecretos;
        CadenaAdministracion = cadenaAdministracion;
        CadenaCatalogo = cadenaCatalogo;
    }

    public string RaizSecretos { get; }
    public string CadenaAdministracion { get; }

    /// <summary>Catálogo de plataforma de este entorno (lo lee el borde común).</summary>
    public string CadenaCatalogo { get; }
    public AlmacenSecretosLocal Secretos => new(RaizSecretos);

    /// <param name="keycloak">Keycloak real; sin él, los realms se registran en memoria.</param>
    /// <param name="azurite">Almacenamiento de objetos real; sin él, los buckets se registran en memoria.</param>
    public static async Task<EntornoAprovisionamiento> CrearAsync(
        PostgresFixture postgres, KeycloakFixture? keycloak = null, AzuriteFixture? azurite = null)
    {
        // La conexión de Testcontainers es de superusuario: hace de rol administrativo del servidor.
        var administracion = postgres.CadenaConexion;
        var catalogo = $"catalogo_{Guid.NewGuid():N}"[..20];
        await EjecutarComoAdministradorAsync(administracion, $"CREATE DATABASE \"{catalogo}\"");
        var cadenaCatalogo = new NpgsqlConnectionStringBuilder(administracion) { Database = catalogo }.ConnectionString;

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalogo"] = cadenaCatalogo,
                ["ConnectionStrings:AdministracionServidor"] = administracion,
                ["FlowRide:Identidad:UrlKeycloak"] = keycloak?.UrlBase ?? "http://keycloak.pruebas.invalid",
                ["FlowRide:Identidad:UrlPanelOperador"] = "https://{operador}.dev.localhost:9443",
                ["FlowRide:Keycloak:Administrador:Usuario"] = keycloak is null ? "admin" : KeycloakFixture.Usuario,
                ["FlowRide:Keycloak:Administrador:Contrasena"] = keycloak is null ? "admin" : KeycloakFixture.Contrasena,
                ["FlowRide:Keycloak:Smtp:Host"] = KeycloakFixture.HostMailpit,
                ["ConnectionStrings:ServidorObjetos"] = azurite?.CadenaConexion ?? "UseDevelopmentStorage=true"
            })
            .Build();

        var raiz = Directory.CreateTempSubdirectory("flowride-secretos-").FullName;
        var coleccion = new ServiceCollection()
            .AddAprovisionamiento(configuracion, raiz)
            .AddSingleton(new FabricaStackOperador(configuracion, raiz));
        if (keycloak is null)
            coleccion.Replace(ServiceDescriptor.Singleton<IServidorIdentidad>(new ServidorIdentidadDePrueba()));
        if (azurite is null)
            coleccion.Replace(ServiceDescriptor.Singleton<IServidorObjetos>(new ServidorObjetosDePrueba()));
        var servicios = coleccion.BuildServiceProvider();

        await using (var alcance = servicios.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<CatalogoDbContext>().Database.MigrateAsync();

        return new EntornoAprovisionamiento(servicios, raiz, administracion, cadenaCatalogo);
    }

    public async Task<AprovisionarOperadorRespuesta> AprovisionarAsync(string tenantId)
    {
        await using var alcance = _servicios.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<IAprovisionarOperadorPuertoEntrada>().AprovisionarAsync(
            new AprovisionarOperadorPeticion { TenantId = tenantId, RazonSocial = $"Operador {tenantId}", Nit = $"nit-{tenantId}", CorreoContacto = $"{tenantId}@ops.co" },
            CancellationToken.None);
    }

    public async Task<T> UsarAsync<T>(Func<IServiceProvider, Task<T>> accion)
    {
        await using var alcance = _servicios.CreateAsyncScope();
        return await accion(alcance.ServiceProvider);
    }

    /// <summary>Cadena de conexión que la herramienta guardó en el almacén de secretos del operador.</summary>
    public async Task<string> CadenaDelSiloAsync(string tenantId) =>
        await Secretos.ObtenerAsync($"flowride-{tenantId}", ClavesSecretos.CadenaConexionSilo, CancellationToken.None)
        ?? throw new InvalidOperationException($"El operador {tenantId} no tiene cadena de conexión guardada.");

    /// <summary>
    /// Contexto del Silo abierto con las credenciales y la clave de cifrado del propio operador, como lo abre su stack.
    /// </summary>
    public async Task<SiloAbierto> AbrirSiloAsync(string tenantId)
    {
        var fuente = NpgsqlDataSource.Create(await CadenaDelSiloAsync(tenantId));
        var opciones = new DbContextOptionsBuilder<FlowRideDbContext>();
        FlowRideDbContext.Configurar(opciones, fuente);
        return new SiloAbierto(new FlowRideDbContext(opciones.Options, await CifradorAsync(tenantId)), fuente);
    }

    /// <summary>Un cifrador por operador (así EF reutiliza su modelo), con la clave de su almacén de secretos.</summary>
    public async Task<CifradorCampos> CifradorAsync(string tenantId)
    {
        if (_cifradores.TryGetValue(tenantId, out var cifrador))
            return cifrador;
        var ajustes = await AjustesDelStackAsync(tenantId);
        return _cifradores.GetOrAdd(tenantId, _ => CifradorCampos.DesdeConfiguracion(
            new ConfigurationBuilder().AddInMemoryCollection(ajustes).Build()));
    }

    /// <summary>Secretos que el stack del operador lee de su almacén: para levantar su API en las pruebas.</summary>
    public async Task<IReadOnlyDictionary<string, string?>> AjustesDelStackAsync(string tenantId)
    {
        var ambito = $"flowride-{tenantId}";
        var ajustes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var clave in new[]
                 {
                     ClavesSecretos.CadenaConexionSilo, ClavesSecretos.SecretoClienteIdentidad, ClavesSecretos.ClaveCifradoV1,
                     ClavesSecretos.VersionCifradoVigente, ClavesSecretos.CadenaConexionObjetos
                 })
        {
            ajustes[clave] = await Secretos.ObtenerAsync(ambito, clave, CancellationToken.None);
        }
        return ajustes;
    }

    public static async Task EjecutarComoAdministradorAsync(string cadenaAdministracion, string sql)
    {
        await using var fuente = NpgsqlDataSource.Create(cadenaAdministracion);
        await using var comando = fuente.CreateCommand(sql);
        await comando.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _servicios.DisposeAsync();
        try
        {
            Directory.Delete(RaizSecretos, recursive: true);
        }
        catch (IOException)
        {
            // La carpeta temporal se limpiará con el sistema; no debe tumbar la prueba.
        }
    }
}

/// <summary>Contexto de un Silo y su fuente de conexiones; al liberarse cierra ambos.</summary>
public sealed class SiloAbierto(FlowRideDbContext contexto, NpgsqlDataSource fuente) : IAsyncDisposable
{
    public FlowRideDbContext Contexto { get; } = contexto;

    public async ValueTask DisposeAsync()
    {
        await Contexto.DisposeAsync();
        await fuente.DisposeAsync();
    }
}
