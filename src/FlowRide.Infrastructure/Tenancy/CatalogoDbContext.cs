using FlowRide.Domain.Plataforma;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>
/// Catálogo de plataforma (<c>flowride_catalogo</c>): operadores registrados y cómo llegar a su Silo.
/// No guarda datos operacionales (ADR-0002). Lo usan la herramienta de aprovisionamiento y el borde común (solo las
/// rutas, ADR-0012), nunca el stack de un operador.
/// </summary>
public sealed class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : DbContext(options)
{
    public const string Esquema = "plataforma";

    public DbSet<Operador> Operadores => Set<Operador>();

    public DbSet<RutaOperador> Rutas => Set<RutaOperador>();

    public static void Configurar(DbContextOptionsBuilder options, NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(options);
        options
            .UseNpgsql(dataSource, npgsql => npgsql.MigrationsHistoryTable(FlowRideDbContext.TablaMigraciones, Esquema))
            .UseSnakeCaseNamingConvention();
    }

    internal static CatalogoDbContext Crear(NpgsqlDataSource dataSource)
    {
        var opciones = new DbContextOptionsBuilder<CatalogoDbContext>();
        Configurar(opciones, dataSource);
        return new CatalogoDbContext(opciones.Options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Esquema);
        modelBuilder.ApplyConfiguration(new OperadorConfiguracion());
        modelBuilder.ApplyConfiguration(new RutaOperadorConfiguracion());
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        ConvencionesComunes.Aplicar(configurationBuilder);
    }

    private sealed class OperadorConfiguracion : IEntityTypeConfiguration<Operador>
    {
        public void Configure(EntityTypeBuilder<Operador> entidad)
        {
            entidad.ToTable("operador");
            entidad.HasKey(operador => operador.Id);
            entidad.Property(operador => operador.Id).HasMaxLength(40).ValueGeneratedNever();
            entidad.Property(operador => operador.RazonSocial).HasMaxLength(150).IsRequired();
            entidad.Property(operador => operador.Nit).HasMaxLength(20).IsRequired();
            entidad.Property(operador => operador.CorreoContacto).HasMaxLength(150).IsRequired();
            entidad.Property(operador => operador.Estado).HasConversion<string>().HasMaxLength(20);
            entidad.Property(operador => operador.CreadoEn);
            entidad.HasIndex(operador => operador.Nit).IsUnique();
        }
    }
}

/// <summary>Ruta del borde por operador (ADR-0012): subdominio y número de WhatsApp únicos.</summary>
internal sealed class RutaOperadorConfiguracion : IEntityTypeConfiguration<RutaOperador>
{
    public void Configure(EntityTypeBuilder<RutaOperador> entidad)
    {
        entidad.ToTable("ruta_operador");
        entidad.HasKey(ruta => ruta.OperadorId);
        entidad.Property(ruta => ruta.OperadorId).HasMaxLength(40).ValueGeneratedNever();
        entidad.Property(ruta => ruta.Subdominio).HasMaxLength(63).IsRequired();
        entidad.Property(ruta => ruta.UrlStack).HasMaxLength(300).IsRequired();
        entidad.Property(ruta => ruta.PhoneNumberId).HasMaxLength(30);
        entidad.Property(ruta => ruta.ActualizadaEn);
        entidad.HasIndex(ruta => ruta.Subdominio).IsUnique();
        entidad.HasIndex(ruta => ruta.PhoneNumberId).IsUnique();
        entidad.HasOne<Operador>().WithOne().HasForeignKey<RutaOperador>(ruta => ruta.OperadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Solo para <c>dotnet ef migrations add</c>.</summary>
public sealed class FabricaDisenoCatalogo : IDesignTimeDbContextFactory<CatalogoDbContext>
{
    public CatalogoDbContext CreateDbContext(string[] args) =>
        CatalogoDbContext.Crear(NpgsqlDataSource.Create("Host=localhost;Database=flowride_diseno"));
}
