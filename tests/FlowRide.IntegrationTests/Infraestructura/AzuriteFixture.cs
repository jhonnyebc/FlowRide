using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Azurite real (la misma versión que docker-compose): almacenamiento de objetos de los documentos (ADR-0013). La cuenta
/// y su clave se generan en cada ejecución (<c>AZURITE_ACCOUNTS</c>), así que el repositorio no guarda ninguna clave.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string Cuenta = "flowridepruebas";
    private readonly string _clave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private readonly IContainer _contenedor;

    public AzuriteFixture() =>
        _contenedor = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
            .WithCommand("azurite-blob", "--blobHost", "0.0.0.0", "--blobPort", "10000", "--loose", "--skipApiVersionCheck")
            .WithEnvironment("AZURITE_ACCOUNTS", $"{Cuenta}:{_clave}")
            .WithPortBinding(10000, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("successfully listens"))
            .Build();

    public string CadenaConexion =>
        $"DefaultEndpointsProtocol=http;AccountName={Cuenta};AccountKey={_clave};" +
        $"BlobEndpoint=http://{_contenedor.Hostname}:{_contenedor.GetMappedPublicPort(10000)}/{Cuenta};";

    public Task InitializeAsync() => _contenedor.StartAsync();

    public Task DisposeAsync() => _contenedor.DisposeAsync().AsTask();
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionAzurite : ICollectionFixture<PostgresFixture>, ICollectionFixture<AzuriteFixture>
{
    public const string Nombre = "azurite";
}
