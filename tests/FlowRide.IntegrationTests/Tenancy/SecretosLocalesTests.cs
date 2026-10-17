using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;

namespace FlowRide.IntegrationTests.Tenancy;

/// <summary>Almacén local de secretos por operador, equivalente a su Key Vault en desarrollo (ADR-0014).</summary>
[Trait("Category", "Integration")]
public sealed class SecretosLocalesTests : IDisposable
{
    private const string Variable = "FLOWRIDE_PRUEBA_SECRETOS_LOCALES__ConnectionStrings__Silo";
    private readonly string _raiz = Directory.CreateTempSubdirectory("flowride-secretos-").FullName;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        Directory.Delete(_raiz, recursive: true);
    }

    [Fact]
    public async Task GuardarYLeer_PorAmbito_SinMezclarOperadores()
    {
        var almacen = new AlmacenSecretosLocal(_raiz);

        await almacen.GuardarAsync("flowride-tenant-a", ClavesSecretos.ContrasenaSilo, "clave-a", CancellationToken.None);
        await almacen.GuardarAsync("flowride-tenant-b", ClavesSecretos.ContrasenaSilo, "clave-b", CancellationToken.None);
        await almacen.GuardarAsync("flowride-tenant-a", ClavesSecretos.ContrasenaSilo, "clave-a2", CancellationToken.None);

        Assert.Equal("clave-a2", await almacen.ObtenerAsync("flowride-tenant-a", ClavesSecretos.ContrasenaSilo, CancellationToken.None));
        Assert.Equal("clave-b", await almacen.ObtenerAsync("flowride-tenant-b", ClavesSecretos.ContrasenaSilo, CancellationToken.None));
        Assert.Null(await almacen.ObtenerAsync("flowride-tenant-c", ClavesSecretos.ContrasenaSilo, CancellationToken.None));
    }

    [Fact]
    public async Task EnElHostDeLaApi_ElAlmacenGanaAAppsettingsYUserSecrets()
    {
        await GuardarCadenasAsync();
        var builder = CrearHost([]);

        builder.Configuration.AddSecretosLocalesDelOperador("tenant-a", _raiz);

        Assert.Equal("desde-almacen", builder.Configuration[ClavesSecretos.CadenaConexionSilo]);
    }

    [Fact]
    public async Task EnElHostDeLaApi_LaLineaDeComandosGanaAlAlmacen()
    {
        await GuardarCadenasAsync();
        var builder = CrearHost([$"--{ClavesSecretos.CadenaConexionSilo}=desde-linea-de-comandos"]);

        builder.Configuration.AddSecretosLocalesDelOperador("tenant-a", _raiz);

        Assert.Equal("desde-linea-de-comandos", builder.Configuration[ClavesSecretos.CadenaConexionSilo]);
    }

    [Fact]
    public async Task SinFuenteGeneralDeEntorno_ElAlmacenQuedaAlFinal()
    {
        await GuardarCadenasAsync();
        Environment.SetEnvironmentVariable(Variable, "desde-entorno-con-prefijo");

        var configuracion = new ConfigurationBuilder()
            .AddEnvironmentVariables("FLOWRIDE_PRUEBA_SECRETOS_LOCALES__")
            .AddSecretosLocalesDelOperador("tenant-a", _raiz)
            .Build();

        Assert.Equal("desde-almacen", configuracion[ClavesSecretos.CadenaConexionSilo]);
    }

    /// <summary>Host igual al de la API, con una fuente que hace de appsettings y user-secrets del proyecto.</summary>
    private static Microsoft.AspNetCore.Builder.WebApplicationBuilder CrearHost(string[] args)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(
            new Microsoft.AspNetCore.Builder.WebApplicationOptions { EnvironmentName = "Development", Args = args });
        var indiceEntorno = builder.Configuration.Sources.ToList().FindLastIndex(fuente =>
            fuente is Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource);
        builder.Configuration.Sources.Insert(indiceEntorno, new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource
        {
            InitialData = [new(ClavesSecretos.CadenaConexionSilo, "desde-user-secrets-del-proyecto")]
        });
        return builder;
    }

    private async Task GuardarCadenasAsync()
    {
        var almacen = new AlmacenSecretosLocal(_raiz);
        await almacen.GuardarAsync("flowride-tenant-a", ClavesSecretos.CadenaConexionSilo, "desde-almacen", CancellationToken.None);
        await almacen.GuardarAsync("flowride-tenant-b", ClavesSecretos.CadenaConexionSilo, "de-otro-operador", CancellationToken.None);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Tenant A")]
    public void TenantInvalido_NoAgregaNingunaFuente(string? tenantId)
    {
        var builder = new ConfigurationBuilder();

        builder.AddSecretosLocalesDelOperador(tenantId, _raiz);

        Assert.Empty(builder.Sources);
    }

    [Fact]
    public void ContrasenasGeneradas_SonDistintasYSegurasEnUnaCadenaDeConexion()
    {
        var generador = new GeneradorSecretosCriptografico();

        var contrasenas = Enumerable.Range(0, 50).Select(_ => generador.NuevaContrasena()).ToArray();

        Assert.Equal(50, contrasenas.Distinct().Count());
        Assert.All(contrasenas, contrasena =>
        {
            Assert.Equal(43, contrasena.Length);
            Assert.DoesNotContain(contrasena, c => c is ';' or '\'' or '"' or ' ' or '=');
        });
    }
}
