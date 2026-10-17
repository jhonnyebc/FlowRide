using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Aprovisionamiento;
using FlowRide.Infrastructure;
using FlowRide.Infrastructure.Persistencia;
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

    private EntornoAprovisionamiento(ServiceProvider servicios, string raizSecretos, string cadenaAdministracion)
    {
        _servicios = servicios;
        RaizSecretos = raizSecretos;
        CadenaAdministracion = cadenaAdministracion;
    }

    public string RaizSecretos { get; }
    public string CadenaAdministracion { get; }
    public AlmacenSecretosLocal Secretos => new(RaizSecretos);

    /// <param name="keycloak">Keycloak real; sin él, los realms se registran en memoria.</param>
    public static async Task<EntornoAprovisionamiento> CrearAsync(PostgresFixture postgres, KeycloakFixture? keycloak = null)
    {
        // La conexión de Testcontainers es de superusuario: hace de rol administrativo del servidor.
        var administracion = postgres.CadenaConexion;
        var catalogo = $"catalogo_{Guid.NewGuid():N}"[..20];
        await EjecutarComoAdministradorAsync(administracion, $"CREATE DATABASE \"{catalogo}\"");

        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalogo"] = new NpgsqlConnectionStringBuilder(administracion) { Database = catalogo }.ConnectionString,
                ["ConnectionStrings:AdministracionServidor"] = administracion,
                ["FlowRide:Identidad:UrlKeycloak"] = keycloak?.UrlBase ?? "http://keycloak.pruebas.invalid",
                ["FlowRide:Keycloak:Administrador:Usuario"] = keycloak is null ? "admin" : KeycloakFixture.Usuario,
                ["FlowRide:Keycloak:Administrador:Contrasena"] = keycloak is null ? "admin" : KeycloakFixture.Contrasena,
                ["FlowRide:Keycloak:Smtp:Host"] = KeycloakFixture.HostMailpit
            })
            .Build();

        var raiz = Directory.CreateTempSubdirectory("flowride-secretos-").FullName;
        var coleccion = new ServiceCollection()
            .AddAprovisionamiento(configuracion, raiz)
            .AddSingleton(new FabricaStackOperador(configuracion, raiz));
        if (keycloak is null)
            coleccion.Replace(ServiceDescriptor.Singleton<IServidorIdentidad>(new ServidorIdentidadDePrueba()));
        var servicios = coleccion.BuildServiceProvider();

        await using (var alcance = servicios.CreateAsyncScope())
            await alcance.ServiceProvider.GetRequiredService<CatalogoDbContext>().Database.MigrateAsync();

        return new EntornoAprovisionamiento(servicios, raiz, administracion);
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

    /// <summary>Contexto del Silo abierto con las credenciales del propio operador, como lo abre su stack.</summary>
    public async Task<SiloAbierto> AbrirSiloAsync(string tenantId)
    {
        var fuente = NpgsqlDataSource.Create(await CadenaDelSiloAsync(tenantId));
        var opciones = new DbContextOptionsBuilder<FlowRideDbContext>();
        FlowRideDbContext.Configurar(opciones, fuente);
        return new SiloAbierto(new FlowRideDbContext(opciones.Options), fuente);
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
