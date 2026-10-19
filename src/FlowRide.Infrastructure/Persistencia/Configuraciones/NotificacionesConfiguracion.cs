using FlowRide.Application.Notificaciones.Catalogo;
using FlowRide.Domain.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

// Esquema notif (ADR-0015, ADR-0016): alertas del panel, correos pendientes y registro de plantillas de WhatsApp.

internal sealed class AlertaConfiguracion : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> entidad)
    {
        entidad.ToTable("alerta", EsquemasMotor.Notificaciones);
        entidad.HasKey(a => a.Id);
        entidad.Property(a => a.Id).ValueGeneratedNever();
        entidad.Property(a => a.Secuencia).UseIdentityAlwaysColumn();
        entidad.Property(a => a.Tipo).HasMaxLength(80).IsRequired();
        entidad.Property(a => a.Severidad).ComoTexto();
        entidad.Property(a => a.Titulo).HasMaxLength(200).IsRequired();
        entidad.Property(a => a.Detalle).IsRequired();
        entidad.ConVersion();

        entidad.HasIndex(a => a.Secuencia).IsUnique();
        entidad.HasIndex(a => a.CreadaEn).HasFilter("leida_en IS NULL");
    }
}

internal sealed class CorreoSalienteConfiguracion : IEntityTypeConfiguration<CorreoSaliente>
{
    public void Configure(EntityTypeBuilder<CorreoSaliente> entidad)
    {
        entidad.ToTable("correo_saliente", EsquemasMotor.Notificaciones);
        entidad.HasKey(c => c.Id);
        entidad.Property(c => c.Id).ValueGeneratedNever();
        entidad.Property(c => c.Destinatario).HasMaxLength(200).IsRequired();
        entidad.Property(c => c.Asunto).HasMaxLength(250).IsRequired();
        entidad.Property(c => c.Cuerpo).IsRequired();
        entidad.Property(c => c.Estado).ComoTexto();
        entidad.ConVersion();

        entidad.HasIndex(c => new { c.Estado, c.ProximoIntento });
        entidad.HasOne<Alerta>().WithMany().HasForeignKey(c => c.AlertaId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Cada clave del catálogo que va a un pasajero o conductor nace con su plantilla Pendiente (se aprueba en Meta).</summary>
internal sealed class PlantillaWhatsAppConfiguracion : IEntityTypeConfiguration<PlantillaWhatsApp>
{
    public const string IdiomaPorDefecto = "es_CO";

    public void Configure(EntityTypeBuilder<PlantillaWhatsApp> entidad)
    {
        entidad.ToTable("plantilla_whatsapp", EsquemasMotor.Notificaciones);
        entidad.HasKey(p => p.Clave);
        entidad.Property(p => p.Clave).HasMaxLength(80);
        entidad.Property(p => p.Nombre).HasMaxLength(512).IsRequired();
        entidad.Property(p => p.Idioma).HasMaxLength(10).IsRequired();
        entidad.Property(p => p.Estado).ComoTexto();
        entidad.ConVersion();

        entidad.HasData(CatalogoMensajes.Cargar().Claves
            .Where(clave => !clave.StartsWith("alerta.", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(clave => new
            {
                Clave = clave,
                Nombre = clave.Replace('.', '_'),
                Idioma = IdiomaPorDefecto,
                Estado = EstadoPlantilla.Pendiente,
                ModificadaPorId = (Guid?)null,
                ModificadaEn = EsquemasMotor.Siembra
            }));
    }
}
