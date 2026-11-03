using FlowRide.Domain.Entidades;
using FlowRide.Domain.Parametros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

/// <summary>
/// ops.parametro (ADR-0016): solo los parámetros que el Administrador cambió; los demás rigen por defecto. La versión
/// del parámetro es también su token de concurrencia: dos cambios simultáneos no se pisan (ADR-0007).
/// </summary>
internal sealed class ParametroConfiguracion : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> entidad)
    {
        entidad.ToTable("parametro", tabla => tabla.HasCheckConstraint("ck_parametro_version", "version > 0"));
        entidad.HasKey(parametro => parametro.Clave);
        entidad.Property(parametro => parametro.Clave).HasMaxLength(100);
        entidad.Property(parametro => parametro.Tipo).ComoTexto();
        entidad.Property(parametro => parametro.Valor).HasColumnType("jsonb").IsRequired();
        entidad.Property(parametro => parametro.Version).IsConcurrencyToken();
        entidad.Property(parametro => parametro.ModificadoPorId);
        entidad.Property(parametro => parametro.ModificadoEn);

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(parametro => parametro.ModificadoPorId).OnDelete(DeleteBehavior.SetNull);
    }
}
