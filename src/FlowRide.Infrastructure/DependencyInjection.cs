using Azure.Storage.Blobs;
using FlowRide.Application.Asignaciones.CasosUso;
using FlowRide.Application.Asignaciones.Entrada.Adaptadores;
using FlowRide.Application.Asignaciones.Entrada.Puertos;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Auditoria.CasosUso;
using FlowRide.Application.Auditoria.Entrada.Adaptadores;
using FlowRide.Application.Auditoria.Entrada.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Identidad.CasosUso;
using FlowRide.Application.Identidad.Entrada.Adaptadores;
using FlowRide.Application.Identidad.Entrada.Puertos;
using FlowRide.Application.Identidad.Salida.Puertos;
using FlowRide.Application.Operacion.CasosUso;
using FlowRide.Application.Operacion.Entrada.Adaptadores;
using FlowRide.Application.Operacion.Entrada.Puertos;
using FlowRide.Application.Operacion.Salida.Puertos;
using FlowRide.Application.Parametros.CasosUso;
using FlowRide.Application.Parametros.Entrada.Adaptadores;
using FlowRide.Application.Parametros.Entrada.Puertos;
using FlowRide.Application.Parametros.Salida.Puertos;
using FlowRide.Application.Seguridad.CasosUso;
using FlowRide.Application.Seguridad.Entrada.Adaptadores;
using FlowRide.Application.Seguridad.Entrada.Puertos;
using FlowRide.Application.Seguridad.Salida.Puertos;
using FlowRide.Application.Solicitudes.CasosUso;
using FlowRide.Application.Solicitudes.Entrada.Adaptadores;
using FlowRide.Application.Solicitudes.Entrada.Puertos;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Asignaciones.Repositorios;
using FlowRide.Domain.Assignment;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Parametros.Repositorios;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Infrastructure.Asignaciones;
using FlowRide.Infrastructure.Auditoria;
using FlowRide.Infrastructure.Identidad;
using FlowRide.Infrastructure.Identidad.Keycloak;
using FlowRide.Infrastructure.Operacion;
using FlowRide.Infrastructure.Parametros;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Persistencia.Outbox;
using FlowRide.Infrastructure.Plataforma;
using FlowRide.Infrastructure.Seguridad;
using FlowRide.Infrastructure.Solicitudes;
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
    public const string CadenaConexionObjetos = "Objetos";
    public const string CadenaConexionServidorObjetos = "ServidorObjetos";
    public const string EtiquetaReadiness = "ready";
    public const string ClaveZonaHoraria = "FlowRide:ZonaHoraria";
    public const string ZonaHorariaPorDefecto = "America/Bogota";
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

        // Clave propia del operador para los campos sensibles (ADR-0014): sin ella el stack no arranca.
        var cifrador = CifradorCampos.DesdeConfiguracion(configuration);
        services.AddSingleton(cifrador);

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddDbContext<FlowRideDbContext>((proveedor, opciones) =>
            FlowRideDbContext.Configurar(opciones, proveedor.GetRequiredService<NpgsqlDataSource>()));

        // Unidad de trabajo y auditoría del Silo (ADR-0007, ADR-0011).
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IUnidadTrabajo, UnidadTrabajoEf>();
        services.AddScoped<IEventoAuditoriaRepositorio, EventoAuditoriaRepositorioAdaptador>();
        services.AddScoped<ICasoUsoConRetorno<VerificarAuditoriaDominio, ResultadoVerificacionAuditoria>, VerificarAuditoriaCasoUso>();
        services.AddScoped<IVerificarAuditoriaPuertoEntrada, VerificarAuditoriaAdaptadorEntrada>();

        // Mantenimiento del cifrado (lo ejecuta la herramienta al aprovisionar).
        services.AddScoped<IRecifradorCampos, RecifradorCamposSilo>();
        services.AddScoped<ICasoUsoConRetorno<AsegurarCifradoDominio, int>, AsegurarCifradoCasoUso>();
        services.AddScoped<IAsegurarCifradoPuertoEntrada, AsegurarCifradoAdaptadorEntrada>();

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
        services.AddSingleton<IServidorObjetos>(new ServidorObjetosAzureBlob(SecretoRequerido(configuration,
            CadenaConexionServidorObjetos, "Es la cuenta de almacenamiento de objetos de la plataforma (Azurite en local).")));

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

    /// <summary>
    /// Registra los recursos operativos y los parámetros del stack del operador (RF-04 a RF-07, RF-26 a RF-28, RF-38,
    /// RF-41; ADR-0006, ADR-0013, ADR-0016). Exige la cadena de su bucket de objetos.
    /// </summary>
    public static IServiceCollection AddOperacion(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var tenantId = ConfiguracionRequerida(configuration, $"{SiloOptions.Seccion}:TenantId", "Es el operador al que sirve este stack.");
        var objetos = SecretoRequerido(configuration, CadenaConexionObjetos,
            "Lo genera la herramienta de aprovisionamiento en el almacén de secretos del operador.");
        var bucket = new BlobContainerClient(objetos, Operador.NombreBucketDe(tenantId));

        // Adaptadores de salida.
        services.AddSingleton<IAlmacenObjetos>(new AlmacenObjetosAzureBlob(bucket));
        services.AddMemoryCache();
        services.AddSingleton(configuration.GetSection(CacheParametrosOptions.Seccion).Get<CacheParametrosOptions>()
                              ?? new CacheParametrosOptions());
        services.AddScoped<ParametroRepositorioAdaptador>();
        services.AddScoped<IParametroRepositorio>(proveedor => proveedor.GetRequiredService<ParametroRepositorioAdaptador>());
        services.AddScoped<ICacheParametros>(proveedor => proveedor.GetRequiredService<ParametroRepositorioAdaptador>());
        services.AddScoped<IEmpresaClienteRepositorio, EmpresaClienteRepositorioAdaptador>();
        services.AddScoped<IPasajeroRepositorio, PasajeroRepositorioAdaptador>();
        services.AddScoped<IConductorRepositorio, ConductorRepositorioAdaptador>();
        services.AddScoped<IVehiculoRepositorio, VehiculoRepositorioAdaptador>();
        services.AddScoped<IAsignacionRepositorio, AsignacionRepositorioAdaptador>();

        // Dominio.
        services.TryAddScoped<UsuarioDominio>();
        services.AddScoped<EmpresaClienteDominio>();
        services.AddScoped<PasajeroDominio>();
        services.AddScoped<ConductorDominio>();
        services.AddScoped<VehiculoDominio>();

        // Casos de uso.
        services.AddScoped<ICasoUsoConRetorno<RegistrarEmpresaClienteDominio, EmpresaCliente>, RegistrarEmpresaClienteCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ActualizarEmpresaClienteDominio, EmpresaCliente>, ActualizarEmpresaClienteCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarEstadoEmpresaClienteDominio, EmpresaCliente>, CambiarEstadoEmpresaClienteCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarEmpresasClienteDominio, PaginaResultado<EmpresaCliente>>, BuscarEmpresasClienteCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<RegistrarPasajeroDominio, Pasajero>, RegistrarPasajeroCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ActualizarPasajeroDominio, Pasajero>, ActualizarPasajeroCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarPasajerosDominio, PaginaResultado<Pasajero>>, BuscarPasajerosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<RegistrarConductorDominio, Conductor>, RegistrarConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ActualizarConductorDominio, Conductor>, ActualizarConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarEstadoConductorDominio, Conductor>, CambiarEstadoConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<Guid, Conductor>, ConsultarConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarConductoresDominio, PaginaResultado<Conductor>>, BuscarConductoresCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ProgramarTurnoDominio, Turno>, ProgramarTurnoCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarDisponibilidadDominio, IReadOnlyList<Conductor>>, ConsultarDisponibilidadCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<SubirDocumentoConductorDominio, DocumentoConductor>, SubirDocumentoConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<DocumentoConductorRefDominio, UrlDocumento>, ObtenerUrlDocumentoConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<DocumentoConductorRefDominio, DocumentoConductor>, EliminarDocumentoConductorCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<RegistrarVehiculoDominio, Vehiculo>, RegistrarVehiculoCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ActualizarVehiculoDominio, Vehiculo>, ActualizarVehiculoCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarEstadoVehiculoDominio, Vehiculo>, CambiarEstadoVehiculoCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<AsignarConductorHabitualDominio, Vehiculo>, AsignarConductorHabitualCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarVehiculosDominio, PaginaResultado<Vehiculo>>, BuscarVehiculosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarParametrosDominio, IReadOnlyList<ParametroVigente>>, ConsultarParametrosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CambiarParametroDominio, ParametroVigente>, CambiarParametroCasoUso>();

        // Adaptadores de entrada.
        services.AddScoped<IEmpresasClientePuertoEntrada, EmpresasClienteAdaptadorEntrada>();
        services.AddScoped<IPasajerosPuertoEntrada, PasajerosAdaptadorEntrada>();
        services.AddScoped<IConductoresPuertoEntrada, ConductoresAdaptadorEntrada>();
        services.AddScoped<IDocumentosConductorPuertoEntrada, DocumentosConductorAdaptadorEntrada>();
        services.AddScoped<IVehiculosPuertoEntrada, VehiculosAdaptadorEntrada>();
        services.AddScoped<IParametrosPuertoEntrada, ParametrosAdaptadorEntrada>();

        return services;
    }

    /// <summary>
    /// Registra las solicitudes e itinerarios del stack del operador (RF-08, RF-09, RF-29 a RF-32): escritura por el
    /// dominio y lectura por proyecciones (CQRS ligero). Requiere <see cref="AddOperacion"/>.
    /// </summary>
    public static IServiceCollection AddSolicitudes(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var nombreZona = configuration[ClaveZonaHoraria] ?? ZonaHorariaPorDefecto;
        TimeZoneInfo zona;
        try
        {
            zona = TimeZoneInfo.FindSystemTimeZoneById(nombreZona);
        }
        catch (TimeZoneNotFoundException error)
        {
            throw new InvalidOperationException($"La zona horaria '{nombreZona}' de '{ClaveZonaHoraria}' no existe.", error);
        }
        services.AddSingleton(new ZonaHorariaOperador(zona));

        // Adaptadores de salida (escritura y lectura).
        services.AddScoped<ISolicitudRepositorio, SolicitudRepositorioAdaptador>();
        services.AddScoped<IItinerarioRepositorio, ItinerarioRepositorioAdaptador>();
        services.AddScoped<IConsultasSolicitudes, ConsultasSolicitudesEf>();

        // Dominio, casos de uso y adaptadores de entrada.
        services.TryAddScoped<ParametrosDominio>();
        services.AddScoped<SolicitudDominio>();
        services.AddScoped<ICasoUsoConRetorno<RegistrarSolicitudDominio, SolicitudViaje>, RegistrarSolicitudCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ModificarSolicitudDominio, SolicitudViaje>, ModificarSolicitudCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CancelarSolicitudDominio, SolicitudViaje>, CancelarSolicitudCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarSolicitudDominio, SolicitudDetalleLectura>, ConsultarSolicitudCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarSolicitudesDominio, PaginaResultado<SolicitudLectura>>, BuscarSolicitudesCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<RegistrarItinerariosDominio, IReadOnlyList<Itinerario>>, RegistrarItinerariosCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarItinerariosDominio, PaginaResultado<Itinerario>>, BuscarItinerariosCasoUso>();
        services.AddScoped<ISolicitudesPuertoEntrada, SolicitudesAdaptadorEntrada>();
        services.AddScoped<IItinerariosPuertoEntrada, ItinerariosAdaptadorEntrada>();

        return services;
    }

    /// <summary>
    /// Registra la asignación de punta a punta (RF-10 a RF-13, RF-21; ADR-0007, ADR-0009): motor con su catálogo,
    /// configuración versionada, decision log, Outbox, casos de uso y el worker de la cola. Requiere <see cref="AddSolicitudes"/>.
    /// </summary>
    public static IServiceCollection AddAsignaciones(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var tenantId = ConfiguracionRequerida(configuration, $"{SiloOptions.Seccion}:TenantId", "Es el operador al que sirve este stack.");
        var catalogo = new CatalogoReglas(DefaultAssignmentRules.Create());
        services.AddSingleton(catalogo);
        services.AddSingleton(new AssignmentEngine(catalogo.Reglas));
        services.AddSingleton(new OperadorDelStack(tenantId));

        // Adaptadores de salida.
        services.AddScoped<IBandejaSalida, BandejaSalidaEf>();
        services.AddScoped<IConfiguracionAsignacionRepositorio, ConfiguracionAsignacionRepositorioAdaptador>();
        services.AddScoped<IDecisionAsignacionRepositorio, DecisionAsignacionRepositorioAdaptador>();
        services.AddScoped<IConsultasAsignaciones, ConsultasAsignacionesEf>();

        // Casos de uso y adaptadores de entrada.
        services.AddScoped<PreparadorDecision>();
        services.AddScoped<ValidadorCandidatoManual>();
        services.AddScoped<ICasoUsoConRetorno<AsignarSolicitudDominio, ResultadoAsignacion>, AsignarSolicitudCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarColaDominio, IReadOnlyList<Guid>>, ConsultarColaCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<AsignarManualDominio, Asignacion>, AsignarManualCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<DevolverAColaDominio, SolicitudViaje>, DevolverAColaCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ReasignarDominio, Asignacion>, ReasignarCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<AbandonarTurnoDominio, IReadOnlyList<Guid>>, AbandonarTurnoCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarDecisionesDominio, IReadOnlyList<DecisionLectura>>, ConsultarDecisionesCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<BuscarAsignacionesDominio, PaginaResultado<AsignacionLectura>>, BuscarAsignacionesCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ConsultarConfiguracionesDominio, IReadOnlyList<ConfiguracionAsignacion>>, ConsultarConfiguracionesCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<CrearConfiguracionDominio, ConfiguracionAsignacion>, CrearConfiguracionCasoUso>();
        services.AddScoped<ICasoUsoConRetorno<ActivarConfiguracionDominio, ConfiguracionAsignacion>, ActivarConfiguracionCasoUso>();
        services.AddScoped<IAsignacionesPuertoEntrada, AsignacionesAdaptadorEntrada>();
        services.AddScoped<IConfiguracionMotorPuertoEntrada, ConfiguracionMotorAdaptadorEntrada>();

        // Worker de la cola (adaptador de entrada en segundo plano); se apaga con FlowRide:Asignacion:Worker:Habilitado=false.
        var worker = configuration.GetSection(WorkerAsignacionOptions.Seccion).Get<WorkerAsignacionOptions>() ?? new WorkerAsignacionOptions();
        services.AddSingleton(worker);
        services.AddSingleton<WorkerColaAsignacion>();
        if (worker.Habilitado)
            services.AddHostedService(proveedor => proveedor.GetRequiredService<WorkerColaAsignacion>());

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
