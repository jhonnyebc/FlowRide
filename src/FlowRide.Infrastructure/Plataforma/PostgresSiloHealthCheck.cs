using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace FlowRide.Infrastructure.Plataforma;

/// <summary>Verifica que la base del Silo del operador acepte conexiones (readiness, ADR-0010).</summary>
public sealed class PostgresSiloHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public const string Nombre = "postgres-silo";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("La base del Silo acepta conexiones.");
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            // La excepción queda en el log técnico; la respuesta HTTP no expone detalles de la conexión.
            return new HealthCheckResult(context.Registration.FailureStatus, "La base del Silo no está disponible.", ex);
        }
    }
}
