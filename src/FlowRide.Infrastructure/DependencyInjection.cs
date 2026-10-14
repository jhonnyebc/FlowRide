using FlowRide.Infrastructure.Plataforma;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowRide.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexionSilo = "Silo";
    public const string EtiquetaReadiness = "ready";

    /// <summary>
    /// Registra la infraestructura del stack del operador. Falla al arrancar si falta un secreto:
    /// nunca continúa con un valor por defecto (ADR-0014).
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<SiloOptions>()
            .Bind(configuration.GetSection(SiloOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString(CadenaConexionSilo);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta el secreto 'ConnectionStrings:{CadenaConexionSilo}'. " +
                "Defínelo con user-secrets o con la variable de entorno ConnectionStrings__Silo.");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

        services.AddHealthChecks()
            .AddCheck<PostgresSiloHealthCheck>(PostgresSiloHealthCheck.Nombre, tags: [EtiquetaReadiness]);

        return services;
    }
}
