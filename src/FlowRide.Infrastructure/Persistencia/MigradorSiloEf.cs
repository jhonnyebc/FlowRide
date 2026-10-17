using FlowRide.Application.Tenancy.Salida.Puertos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace FlowRide.Infrastructure.Persistencia;

/// <summary>
/// Aplica las migraciones de EF Core a la base de un Silo con las credenciales del propio Silo.
/// La versión de esquema queda registrada en <c>ops.__ef_migraciones</c> de cada base (ADR-0002).
/// </summary>
public sealed class MigradorSiloEf : IMigradorSilo
{
    public async Task<IReadOnlyList<string>> MigrarAsync(string cadenaConexion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);

        await using var dataSource = NpgsqlDataSource.Create(cadenaConexion);
        await using var contexto = CrearContexto(dataSource);

        var pendientes = (await contexto.Database.GetPendingMigrationsAsync(ct)).ToArray();
        if (pendientes.Length > 0)
            await contexto.Database.MigrateAsync(ct);

        return pendientes;
    }

    internal static FlowRideDbContext CrearContexto(NpgsqlDataSource dataSource)
    {
        var opciones = new DbContextOptionsBuilder<FlowRideDbContext>();
        FlowRideDbContext.Configurar(opciones, dataSource);
        return new FlowRideDbContext(opciones.Options);
    }
}

/// <summary>Solo para <c>dotnet ef migrations add</c>: genera el modelo sin conectarse a ninguna base.</summary>
public sealed class FabricaDisenoFlowRide : IDesignTimeDbContextFactory<FlowRideDbContext>
{
    public FlowRideDbContext CreateDbContext(string[] args) =>
        MigradorSiloEf.CrearContexto(NpgsqlDataSource.Create("Host=localhost;Database=flowride_diseno"));
}
