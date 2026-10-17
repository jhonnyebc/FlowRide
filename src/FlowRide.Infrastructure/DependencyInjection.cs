using FlowRide.Application.Auditoria.CasosUso;
using FlowRide.Application.Auditoria.Entrada.Adaptadores;
using FlowRide.Application.Auditoria.Entrada.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Identidad.CasosUso;
using FlowRide.Application.Identidad.Entrada.Adaptadores;
using FlowRide.Application.Identidad.Entrada.Puertos;
using FlowRide.Application.Identidad.Salida.Puertos;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Infrastructure.Auditoria;
using FlowRide.Infrastructure.Identidad;
using FlowRide.Infrastructure.Identidad.Keycloak;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Plataforma;
using FlowRide.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace FlowRide.Infrastructure;

public static class DependencyInjection
{
    public const string CadenaConexionSilo = "Silo";
    public const string CadenaConexionCatalogo = "Catalogo";
    public const string CadenaConexionAdministracion = "AdministracionServidor";
    public const string EtiquetaReadiness = "ready";
    public const string ClaveUsuarioAdministradorKeycloak = "FlowRide:Keycloak:Administrador:Usuario";
    public const string ClaveContrasenaAdministradorKeycloak = "FlowRide:Keycloak:Administrador:Contrasena";

    private const string ClaveKeycloakPlataforma = "keycloak-plataforma";
    private const string ClaveKeycloakOperador = "keycloak-operador";

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

        // Unidad de trabajo y auditoría del Silo (ADR-0007, ADR-0011).
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IUnidadTrabajo, UnidadTrabajoEf>();
        services.AddScoped<IEventoAuditoriaRepositorio, EventoAuditoriaRepositorioAdaptador>();
        services.AddScoped<ICasoUsoConRetorno<VerificarAuditoriaDominio, ResultadoVerificacionAuditoria>, VerificarAuditoriaCasoUso>();
        services.AddScoped<IVerificarAuditoriaPuertoEntrada, VerificarAuditoriaAdaptadorEntrada>();

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
        services.TryAddSingleton(TimeProvider.System);

        // Keycloak con la cuenta de administración de la plataforma: solo para crear y actualizar realms.
        var identidad = OpcionesIdentidad(configuration);
        var administradorKeycloak = CredencialesKeycloak.Administrador(
            ConfiguracionRequerida(configuration, ClaveUsuarioAdministradorKeycloak,
                "Es el administrador de Keycloak de la plataforma (realm master)."),
            ConfiguracionRequerida(configuration, ClaveContrasenaAdministradorKeycloak,
                "Es la contraseña del administrador de Keycloak de la plataforma."));
        var smtp = configuration.GetSection(SmtpKeycloakOptions.Seccion).Get<SmtpKeycloakOptions>() ?? new SmtpKeycloakOptions();
        services.AddKeyedSingleton(ClaveKeycloakPlataforma, (proveedor, _) =>
            new ClienteKeycloak(identidad.UrlKeycloak, administradorKeycloak, proveedor.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IServidorIdentidad>(proveedor => new ServidorIdentidadKeycloak(
            proveedor.GetRequiredKeyedService<ClienteKeycloak>(ClaveKeycloakPlataforma), identidad, smtp));

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

    /// <summary>
    /// Registra la identidad del stack del operador (RF-01, RF-02, ADR-0003): cuentas del Panel Web en su realm,
    /// usuarios internos con su rol y el mapeo token → usuario. Exige el secreto del cliente de servicio del realm.
    /// </summary>
    public static IServiceCollection AddIdentidad(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<IdentidadOptions>()
            .Bind(configuration.GetSection(IdentidadOptions.Seccion))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var identidad = OpcionesIdentidad(configuration);
        var tenantId = ConfiguracionRequerida(configuration, $"{SiloOptions.Seccion}:TenantId",
            "Es el operador al que sirve este stack.");
        var secretoCliente = ConfiguracionRequerida(configuration, ClavesSecretos.SecretoClienteIdentidad,
            "Lo genera la herramienta de aprovisionamiento en el almacén de secretos del operador.");
        var realm = Operador.NombreRealmDe(tenantId);

        // Adaptadores de salida: la cuenta de servicio del realm del operador, nunca el administrador de la plataforma.
        services.TryAddSingleton(TimeProvider.System);
        services.AddKeyedSingleton(ClaveKeycloakOperador, (proveedor, _) => new ClienteKeycloak(identidad.UrlKeycloak,
            CredencialesKeycloak.CuentaServicio(realm, secretoCliente), proveedor.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IProveedorIdentidad>(proveedor => new ProveedorIdentidadKeycloak(
            proveedor.GetRequiredKeyedService<ClienteKeycloak>(ClaveKeycloakOperador), realm, identidad));
        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorioAdaptador>();

        // Dominio, casos de uso y adaptadores de entrada.
        services.AddScoped<UsuarioDominio>();
        services.AddScoped<ICasoUsoConRetorno<CrearUsuarioDominio, Usuario>, CrearUsuarioCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CrearUsuarioDominio, ResultadoAdministradorInicial>, AsegurarAdministradorInicialCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarEstadoUsuarioDominio, Usuario>, CambiarEstadoUsuarioCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarRolUsuarioDominio, Usuario>, CambiarRolUsuarioCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarUsuariosDominio, IReadOnlyList<Usuario>>, ConsultarUsuariosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ResolverIdentidadDominio, Usuario?>, ResolverIdentidadCasoUso>();
        services.AddScoped<ICrearUsuarioPuertoEntrada, CrearUsuarioAdaptadorEntrada>();
        services.AddScoped<IAsegurarAdministradorInicialPuertoEntrada, AsegurarAdministradorInicialAdaptadorEntrada>();
        services.AddScoped<ICambiarEstadoUsuarioPuertoEntrada, CambiarEstadoUsuarioAdaptadorEntrada>();
        services.AddScoped<ICambiarRolUsuarioPuertoEntrada, CambiarRolUsuarioAdaptadorEntrada>();
        services.AddScoped<IConsultarUsuariosPuertoEntrada, ConsultarUsuariosAdaptadorEntrada>();
        services.AddScoped<IResolverIdentidadPuertoEntrada, ResolverIdentidadAdaptadorEntrada>();

        return services;
    }

    private static IdentidadOptions OpcionesIdentidad(IConfiguration configuration)
    {
        var opciones = configuration.GetSection(IdentidadOptions.Seccion).Get<IdentidadOptions>() ?? new IdentidadOptions();
        if (!Uri.TryCreate(opciones.UrlKeycloak, UriKind.Absolute, out _))
            throw new InvalidOperationException(
                $"Falta la configuración '{IdentidadOptions.Seccion}:UrlKeycloak' (URL base de Keycloak, p. ej. http://localhost:8080).");
        return opciones;
    }

    private static string ConfiguracionRequerida(IConfiguration configuration, string clave, string ayuda)
    {
        var valor = configuration[clave];
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Falta el secreto o la configuración '{clave}'. {ayuda}");
        return valor;
    }

    private static string SecretoRequerido(IConfiguration configuration, string nombre, string ayuda)
    {
        var valor = configuration.GetConnectionString(nombre);
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Falta el secreto 'ConnectionStrings:{nombre}'. {ayuda}");
        return valor;
    }
}
