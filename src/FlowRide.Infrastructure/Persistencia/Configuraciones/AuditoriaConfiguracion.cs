using FlowRide.Domain.Auditoria;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// audit.evento_auditoria (ADR-0011): solo INSERT (un trigger de la migración rechaza UPDATE, DELETE y TRUNCATE) y
/// sin llaves foráneas, para que la auditoría sobreviva a la supresión de datos personales.
/// </summary>
internal sealed class EventoAuditoriaConfiguracion : IEntityTypeConfiguration<EventoAuditoria>
{
    public const string Esquema = "audit";

    public void Configure(EntityTypeBuilder<EventoAuditoria> entidad)
    {
        entidad.ToTable("evento_auditoria", Esquema);
        entidad.HasKey(evento => evento.Id);
        entidad.Property(evento => evento.Id).UseIdentityByDefaultColumn();
        entidad.Property(evento => evento.ActorId);
        entidad.Property(evento => evento.Accion).HasMaxLength(80).IsRequired();
        entidad.Property(evento => evento.Recurso).HasMaxLength(80).IsRequired();
        entidad.Property(evento => evento.RecursoId).HasMaxLength(64).IsRequired();
        entidad.Property(evento => evento.FechaHora);
        entidad.Property(evento => evento.Resultado).ComoTexto();
        entidad.Property(evento => evento.Motivo);
        entidad.Property(evento => evento.ValoresAntes).HasColumnType("jsonb");
        entidad.Property(evento => evento.ValoresDespues).HasColumnType("jsonb");
        entidad.Property(evento => evento.HashPrevio).HasColumnType("char(64)").IsRequired();
        entidad.Property(evento => evento.Hash).HasColumnType("char(64)").IsRequired();
        entidad.Ignore(evento => evento.Sellado);

        entidad.HasIndex(evento => evento.Hash).IsUnique();
        entidad.HasIndex(evento => new { evento.Recurso, evento.RecursoId });
    }
}
