using Testcontainers.PostgreSql;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// PostgreSQL 17 real en un contenedor, compartido por las pruebas de la colección. Cada API, entorno y réplica de prueba
/// guarda su propio pool de conexiones, así que se amplía el máximo de conexiones del servidor (100 por defecto).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:17")
        .WithCommand("-c", "max_connections=500")
        .Build();

    public string CadenaConexion => _contenedor.GetConnectionString();

    public Task InitializeAsync() => _contenedor.StartAsync();

    public Task DisposeAsync() => _contenedor.DisposeAsync().AsTask();
}

[CollectionDefinition(Nombre)]
public sealed class ColeccionPostgres : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "postgres";
}
