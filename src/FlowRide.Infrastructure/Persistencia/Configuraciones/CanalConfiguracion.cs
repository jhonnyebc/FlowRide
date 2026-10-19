using FlowRide.Domain.Canal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

// Canal WhatsApp (esquema canal, ADR-0008): bandeja de entrada idempotente, bandeja de salida con reintentos y
// correlación, y la conversación de cada número con el bot. Los teléfonos van cifrados (ver FlowRideDbContext).

internal static class EsquemaCanal
{
    public const string Canal = "canal";
    public const int LargoIdExterno = 128;
}

internal sealed class MensajeEntranteConfiguracion : IEntityTypeConfiguration<MensajeEntrante>
{
    public void Configure(EntityTypeBuilder<MensajeEntrante> entidad)
    {
        entidad.ToTable("mensaje_entrante", EsquemaCanal.Canal);
        entidad.HasKey(m => m.Id);
        entidad.Property(m => m.Id).ValueGeneratedNever();
        entidad.Property(m => m.IdExterno).HasMaxLength(EsquemaCanal.LargoIdExterno).IsRequired();
        entidad.Property(m => m.Remitente).HasMaxLength(Mapeo.LargoCifrado).IsRequired();
        entidad.Property(m => m.Tipo).HasMaxLength(Mapeo.LargoEnum).IsRequired();
        entidad.Property(m => m.Contenido).IsRequired();
        entidad.Property(m => m.Estado).ComoTexto();
        entidad.ConVersion();

        // Idempotencia del webhook (PoC 4): un mensaje del proveedor se guarda una sola vez.
        entidad.HasIndex(m => m.IdExterno).IsUnique();
        entidad.HasIndex(m => new { m.Estado, m.RecibidoEn });
    }
}

internal sealed class MensajeSalienteConfiguracion : IEntityTypeConfiguration<MensajeSaliente>
{
    public void Configure(EntityTypeBuilder<MensajeSaliente> entidad)
    {
        entidad.ToTable("mensaje_saliente", EsquemaCanal.Canal);
        entidad.HasKey(m => m.Id);
        entidad.Property(m => m.Id).ValueGeneratedNever();
        entidad.Property(m => m.Destinatario).HasMaxLength(Mapeo.LargoCifrado).IsRequired();
        entidad.Property(m => m.Contenido).IsRequired();
        entidad.Property(m => m.Estado).ComoTexto();
        entidad.Property(m => m.IdExterno).HasMaxLength(EsquemaCanal.LargoIdExterno);
        entidad.Property(m => m.EstadoExterno).HasMaxLength(Mapeo.LargoEnum);
        entidad.ConVersion();

        entidad.HasIndex(m => new { m.Estado, m.ProximoIntento });
        entidad.HasIndex(m => m.IdExterno);
        entidad.HasOne<MensajeEntrante>().WithMany().HasForeignKey(m => m.EnRespuestaA).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ConversacionConfiguracion : IEntityTypeConfiguration<Conversacion>
{
    public void Configure(EntityTypeBuilder<Conversacion> entidad)
    {
        entidad.ToTable("conversacion", EsquemaCanal.Canal);
        entidad.HasKey(c => c.Id);
        entidad.Property(c => c.Id).ValueGeneratedNever();
        entidad.Property(c => c.Telefono).HasMaxLength(Mapeo.LargoCifrado).IsRequired();
        entidad.Property(c => c.Paso).HasConversion<string>().HasMaxLength(40);

        // Texto y no jsonb: jsonb reordena y reformatea, y EF vería un cambio en cada lectura.
        entidad.Property(c => c.Datos).IsRequired();
        entidad.ConVersion();

        entidad.HasIndex(c => c.Telefono).IsUnique();
    }
}
