using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Entidades;
using FlowRide.Infrastructure.Persistencia.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

// Esquema motor (ADR-0009): configuración versionada del motor y decision log persistente. Esquema notif: Outbox (ADR-0007).

internal static class EsquemasMotor
{
    public const string Motor = "motor";
    public const string Notificaciones = "notif";

    /// <summary>Fecha fija de la configuración inicial sembrada por la migración.</summary>
    public static readonly DateTimeOffset Siembra = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>Solo una configuración activa: restricción EXCLUDE diferible en la migración (permite archivar y activar en una transacción).</summary>
internal sealed class ConfiguracionAsignacionConfiguracion : IEntityTypeConfiguration<ConfiguracionAsignacion>
{
    public void Configure(EntityTypeBuilder<ConfiguracionAsignacion> entidad)
    {
        entidad.ToTable("configuracion_asignacion", EsquemasMotor.Motor, tabla => tabla.HasCheckConstraint("ck_configuracion_version", "version > 0"));
        entidad.HasKey(c => c.Id);
        entidad.Property(c => c.Id).ValueGeneratedNever();
        entidad.Property(c => c.Version);
        entidad.Property(c => c.Estado).ComoTexto();
        entidad.Property(c => c.CreadaPorId);
        entidad.Property(c => c.CreadaEn);
        entidad.Property(c => c.ActivadaEn);
        entidad.HasIndex(c => c.Version).IsUnique();
        entidad.HasOne<Usuario>().WithMany().HasForeignKey(c => c.CreadaPorId).OnDelete(DeleteBehavior.SetNull);

        entidad.HasMany(c => c.Reglas).WithOne().HasForeignKey(r => r.ConfiguracionId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(c => c.Reglas).UsePropertyAccessMode(PropertyAccessMode.Field);

        entidad.HasData(new
        {
            ConfiguracionInicial.Id,
            Version = ConfiguracionInicial.Version,
            Estado = EstadoConfiguracion.Activa,
            CreadaPorId = (Guid?)null,
            CreadaEn = EsquemasMotor.Siembra,
            ActivadaEn = (DateTimeOffset?)EsquemasMotor.Siembra
        });
    }
}

internal sealed class ReglaConfiguradaConfiguracion : IEntityTypeConfiguration<ReglaConfigurada>
{
    public void Configure(EntityTypeBuilder<ReglaConfigurada> entidad)
    {
        entidad.ToTable("regla_configurada", EsquemasMotor.Motor, tabla =>
        {
            tabla.HasCheckConstraint("ck_regla_peso", "peso BETWEEN 0 AND 1");
            tabla.HasCheckConstraint("ck_regla_orden", "orden > 0");
        });
        entidad.HasKey(r => r.Id);
        entidad.Property(r => r.Id).ValueGeneratedNever();
        entidad.Property(r => r.ConfiguracionId);
        entidad.Property(r => r.CodigoRegla).HasMaxLength(60).IsRequired();
        entidad.Property(r => r.Tipo).ComoTexto();
        entidad.Property(r => r.Habilitada);
        entidad.Property(r => r.Orden).HasColumnType("smallint");
        entidad.Property(r => r.Peso).HasPrecision(5, 4);
        entidad.Property(r => r.Parametros).HasColumnType("jsonb");
        entidad.HasIndex(r => new { r.ConfiguracionId, r.CodigoRegla }).IsUnique();
        entidad.HasIndex(r => new { r.ConfiguracionId, r.Tipo, r.Orden }).IsUnique();

        var catalogo = DefaultAssignmentRules.Create().ToDictionary(regla => regla.Name, regla => regla.Kind, StringComparer.Ordinal);
        entidad.HasData(ConfiguracionInicial.Reglas.Select((regla, indice) => new
        {
            Id = new Guid($"b2000000-0000-0000-0000-{indice + 1:D12}"),
            ConfiguracionId = ConfiguracionInicial.Id,
            CodigoRegla = regla.Codigo,
            Tipo = catalogo[regla.Codigo],
            regla.Habilitada,
            regla.Orden,
            regla.Peso,
            Parametros = new ReglaConfigurada(Guid.Empty, Guid.Empty, regla.Codigo, RuleKind.Filtering, true, 1, 0m, regla.Parametros).Parametros
        }));
    }
}

/// <summary>Decision log: solo INSERT (trigger en la migración), como la auditoría.</summary>
internal sealed class DecisionAsignacionConfiguracion : IEntityTypeConfiguration<DecisionAsignacion>
{
    public void Configure(EntityTypeBuilder<DecisionAsignacion> entidad)
    {
        entidad.ToTable("decision_asignacion", EsquemasMotor.Motor);
        entidad.HasKey(d => d.Id);
        entidad.Property(d => d.Id).ValueGeneratedNever();
        entidad.Property(d => d.SolicitudId);
        entidad.Property(d => d.ConfiguracionId);
        entidad.Property(d => d.VersionConfiguracion);
        entidad.Property(d => d.Origen).ComoTexto();
        entidad.Property(d => d.EvaluadaEn);
        entidad.Property(d => d.Resultado).HasConversion<string>().HasMaxLength(30);
        entidad.Property(d => d.ConductorSeleccionadoId);
        entidad.Property(d => d.VehiculoSeleccionadoId);
        entidad.Property(d => d.Motivo).IsRequired();
        entidad.Property(d => d.Desempate).HasColumnType("jsonb");
        entidad.Property(d => d.EntradaSnapshot).HasColumnType("jsonb").IsRequired();
        entidad.HasIndex(d => new { d.SolicitudId, d.EvaluadaEn });
        entidad.HasOne<SolicitudViaje>().WithMany().HasForeignKey(d => d.SolicitudId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<ConfiguracionAsignacion>().WithMany().HasForeignKey(d => d.ConfiguracionId).OnDelete(DeleteBehavior.Restrict);

        entidad.HasMany(d => d.Evaluaciones).WithOne().HasForeignKey(e => e.DecisionId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(d => d.Evaluaciones).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EvaluacionCandidatoConfiguracion : IEntityTypeConfiguration<EvaluacionCandidato>
{
    public void Configure(EntityTypeBuilder<EvaluacionCandidato> entidad)
    {
        entidad.ToTable("evaluacion_candidato", EsquemasMotor.Motor);
        entidad.HasKey(e => e.Id);
        entidad.Property(e => e.Id).ValueGeneratedNever();
        entidad.Property(e => e.DecisionId);
        entidad.Property(e => e.ConductorId);
        entidad.Property(e => e.VehiculoId);
        entidad.Property(e => e.TurnoId);
        entidad.Property(e => e.Elegible);
        entidad.Property(e => e.MotivoDescarte);
        entidad.Property(e => e.Prioridad).HasPrecision(9, 4);
        entidad.Property(e => e.PuntajeTotal).HasPrecision(9, 4);
        entidad.Property(e => e.PosicionRanking);
        entidad.HasIndex(e => new { e.DecisionId, e.ConductorId, e.VehiculoId }).IsUnique();

        entidad.HasMany(e => e.Resultados).WithOne().HasForeignKey(r => r.EvaluacionId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(e => e.Resultados).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ResultadoReglaConfiguracion : IEntityTypeConfiguration<ResultadoRegla>
{
    public void Configure(EntityTypeBuilder<ResultadoRegla> entidad)
    {
        entidad.ToTable("resultado_regla", EsquemasMotor.Motor);
        entidad.HasKey(r => r.Id);
        entidad.Property(r => r.Id).ValueGeneratedNever();
        entidad.Property(r => r.EvaluacionId);
        entidad.Property(r => r.CodigoRegla).HasMaxLength(60).IsRequired();
        entidad.Property(r => r.VersionRegla);
        entidad.Property(r => r.Tipo).ComoTexto();
        entidad.Property(r => r.Orden).HasColumnType("smallint");
        entidad.Property(r => r.Aprobado);
        entidad.Property(r => r.Valor).HasPrecision(9, 4);
        entidad.Property(r => r.Peso).HasPrecision(5, 4);
        entidad.Property(r => r.ValorPonderado).HasPrecision(9, 4);
        entidad.Property(r => r.Motivo).IsRequired();
        entidad.HasIndex(r => new { r.EvaluacionId, r.CodigoRegla }).IsUnique();
    }
}

/// <summary>notif.outbox_mensaje (ADR-0007): eventos por publicar, escritos en la transacción que los produce.</summary>
internal sealed class MensajeOutboxConfiguracion : IEntityTypeConfiguration<MensajeOutbox>
{
    public void Configure(EntityTypeBuilder<MensajeOutbox> entidad)
    {
        entidad.ToTable("outbox_mensaje", EsquemasMotor.Notificaciones);
        entidad.HasKey(m => m.Id);
        entidad.Property(m => m.Id).ValueGeneratedNever();
        entidad.Property(m => m.EventId);
        entidad.Property(m => m.TipoEvento).HasMaxLength(80).IsRequired();
        entidad.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        entidad.Property(m => m.OcurridoEn);
        entidad.Property(m => m.ProximoIntento);
        entidad.Property(m => m.Intentos).HasColumnType("smallint");
        entidad.Property(m => m.ProcesadoEn);
        entidad.Property(m => m.UltimoError);
        entidad.HasIndex(m => m.EventId).IsUnique();
        entidad.HasIndex(m => m.ProximoIntento).HasFilter("procesado_en IS NULL");
    }
}
