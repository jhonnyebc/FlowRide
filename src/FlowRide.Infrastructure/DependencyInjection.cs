using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Plataforma;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowRide.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexionSilo = "Silo";
    public const string CadenaConexionCatalogo = "Catalogo";
    public const string CadenaConexionAdministracion = "AdministracionServidor";
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

        // El stack sirve a un único operador: su Silo es el de la configuración del despliegue, no uno que elija
        // el cliente. Sin cadena de conexión el stack no arranca (falla cerrado, ADR-0002).
        var connectionString = SecretoRequerido(configuration, CadenaConexionSilo,
            "Defínelo con user-secrets, con el almacén local del operador (herramienta de aprovisionamiento) " +
            "o con la variable de entorno ConnectionStrings__Silo.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<FlowRideDbContext>((proveedor, opciones) =>
            FlowRideDbContext.Configurar(opciones, proveedor.GetRequiredService<NpgsqlDataSource>()));

        services.AddHealthChecks()
            .AddCheck<PostgresSiloHealthCheck>(PostgresSiloHealthCheck.Nombre, tags: [EtiquetaReadiness]);

        return services;
    }

    /// <summary>
    /// Registra lo que necesita la herramienta de aprovisionamiento (RF-03): catálogo de plataforma, servidor de
    /// Silos, migrador y almacén de secretos. Exige sus dos secretos de plataforma (ADR-0014).
    /// </summary>
    public static IServiceCollection AddAprovisionamiento(
        this IServiceCollection services,
        IConfiguration configuration,
        string? raizSecretos = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var catalogo = SecretoRequerido(configuration, CadenaConexionCatalogo,
            "Es la cadena del rol del catálogo de plataforma (flowride_catalogo).");
        var administracion = SecretoRequerido(configuration, CadenaConexionAdministracion,
            "Es la cadena de un rol con permiso para crear roles y bases en el servidor de los Silos.");

        var fuenteCatalogo = NpgsqlDataSource.Create(catalogo);
        var fuenteAdministracion = NpgsqlDataSource.Create(administracion);
        // Adaptadores de salida.
        services.AddDbContext<CatalogoDbContext>(opciones => CatalogoDbContext.Configurar(opciones, fuenteCatalogo));
        services.AddScoped<IOperadorRepositorio, OperadorRepositorioAdaptador>();
        services.AddSingleton<IServidorSilos>(new ServidorSilosPostgres(fuenteAdministracion));
        services.AddSingleton<IMigradorSilo, MigradorSiloEf>();
        services.AddSingleton<IAlmacenSecretos>(_ => new AlmacenSecretosLocal(raizSecretos));
        services.AddSingleton<IGeneradorSecretos, GeneradorSecretosCriptografico>();
        services.AddSingleton(TimeProvider.System);

        // Dominio, casos de uso y adaptadores de entrada.
        services.AddScoped<OperadorDominio>();
        services.AddScoped<ICasoUsoConRetorno<AprovisionarOperadorDominio, ResultadoAprovisionamiento>, AprovisionarOperadorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<MigrarSilosDominio, IReadOnlyList<ResultadoMigracion>>, MigrarSilosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<EstadoOperador?, IReadOnlyList<Operador>>, ConsultarOperadoresCasoUso>();
        services.AddScoped<IAprovisionarOperadorPuertoEntrada, AprovisionarOperadorAdaptadorEntrada>();
        services.AddScoped<IMigrarSilosPuertoEntrada, MigrarSilosAdaptadorEntrada>();
        services.AddScoped<IConsultarOperadoresPuertoEntrada, ConsultarOperadoresAdaptadorEntrada>();

        return services;
    }

    private static string SecretoRequerido(IConfiguration configuration, string nombre, string ayuda)
    {
        var valor = configuration.GetConnectionString(nombre);
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Falta el secreto 'ConnectionStrings:{nombre}'. {ayuda}");
        return valor;
    }
}
