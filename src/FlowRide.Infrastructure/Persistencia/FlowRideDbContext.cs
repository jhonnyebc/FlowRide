using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Parametros;
using FlowRide.Infrastructure.Persistencia.Configuraciones;
using FlowRide.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;

namespace FlowRide.Infrastructure.Persistencia;

/// <summary>
/// Base operativa del Silo de un operador (ADR-0002, ADR-0004). Cada stack la abre con las credenciales de su propio
/// rol, así que una instancia de este contexto nunca puede ver datos de otro operador.
/// </summary>
public sealed class FlowRideDbContext(DbContextOptions<FlowRideDbContext> options, CifradorCampos? cifrador = null)
    : DbContext(options)
{
    public const string Esquema = "ops";
    public const string TablaMigraciones = "__ef_migraciones";

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Pasajero> Pasajeros => Set<Pasajero>();
    public DbSet<Conductor> Conductores => Set<Conductor>();
    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();
    public DbSet<EmpresaCliente> EmpresasCliente => Set<EmpresaCliente>();
    public DbSet<Itinerario> Itinerarios => Set<Itinerario>();
    public DbSet<SolicitudViaje> Solicitudes => Set<SolicitudViaje>();
    public DbSet<Asignacion> Asignaciones => Set<Asignacion>();
    public DbSet<Viaje> Viajes => Set<Viaje>();
    public DbSet<Calificacion> Calificaciones => Set<Calificacion>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<Parametro> Parametros => Set<Parametro>();

    /// <summary>
    /// Cifrador de los campos sensibles del operador (ADR-0014). Sin él (migraciones, diseño) las columnas se leen y
    /// escriben tal cual; el modelo se cachea por cifrador para no mezclar claves.
    /// </summary>
    internal CifradorCampos? Cifrador { get; } = cifrador;

    /// <summary>Opciones comunes del contexto del Silo, para la API, la herramienta de aprovisionamiento y las pruebas.</summary>
    public static void Configurar(DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        options
            .UseNpgsql(dataSource, npgsql => npgsql.MigrationsHistoryTable(TablaMigraciones, Esquema))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, ModeloPorCifrador>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);

        // Restricciones EXCLUDE de turnos y asignaciones (sin franjas solapadas, ADR-0007).
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfiguration(new RolConfiguracion());
        modelBuilder.ApplyConfiguration(new UsuarioConfiguracion());
        modelBuilder.ApplyConfiguration(new PasajeroConfiguracion());
        modelBuilder.ApplyConfiguration(new ConductorConfiguracion());
        modelBuilder.ApplyConfiguration(new TurnoConfiguracion());
        modelBuilder.ApplyConfiguration(new DocumentoConductorConfiguracion());
        modelBuilder.ApplyConfiguration(new VehiculoConfiguracion());
        modelBuilder.ApplyConfiguration(new EmpresaClienteConfiguracion());
        modelBuilder.ApplyConfiguration(new ItinerarioConfiguracion());
        modelBuilder.ApplyConfiguration(new SolicitudViajeConfiguracion());
        modelBuilder.ApplyConfiguration(new EstadoSolicitudHistorialConfiguracion());
        modelBuilder.ApplyConfiguration(new CancelacionSolicitudConfiguracion());
        modelBuilder.ApplyConfiguration(new AsignacionConfiguracion());
        modelBuilder.ApplyConfiguration(new ViajeConfiguracion());
        modelBuilder.ApplyConfiguration(new ParadaConfiguracion());
        modelBuilder.ApplyConfiguration(new IncidenteConfiguracion());
        modelBuilder.ApplyConfiguration(new CalificacionConfiguracion());
        modelBuilder.ApplyConfiguration(new EventoAuditoriaConfiguracion());
        modelBuilder.ApplyConfiguration(new ParametroConfiguracion());

        if (Cifrador is { } cifradorCampos)
        {
            var conversion = new ValueConverter<string, string>(
                valor => cifradorCampos.Cifrar(valor), valor => cifradorCampos.Descifrar(valor));
            modelBuilder.Entity<Usuario>().Property(usuario => usuario.TelefonoWhatsApp).HasConversion(conversion);
            modelBuilder.Entity<Conductor>().Property(conductor => conductor.NumeroLicencia).HasConversion(conversion);
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ConvencionesComunes.Aplicar(configurationBuilder);
    }
}

/// <summary>Convenciones compartidas por los contextos de FlowRide.</summary>
internal static class ConvencionesComunes
{
    public static void Aplicar(ModelConfigurationBuilder configurationBuilder)
    {
        // timestamptz solo admite offset 0 en Npgsql: se guarda en UTC y se lee en UTC.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<ConvertidorUtc>();
    }

    private sealed class ConvertidorUtc() : ValueConverter<DateTimeOffset, DateTimeOffset>(
        valor => valor.ToUniversalTime(),
        valor => valor.ToUniversalTime());
}

/// <summary>Un modelo por cifrador: dos stacks en el mismo proceso (la herramienta) nunca comparten conversiones.</summary>
internal sealed class ModeloPorCifrador : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as FlowRideDbContext)?.Cifrador, designTime);
}
