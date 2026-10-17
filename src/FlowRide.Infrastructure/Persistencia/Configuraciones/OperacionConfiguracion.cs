using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

/// <summary>Agregado SolicitudViaje: incluye su historial de estados y su cancelación.</summary>
internal sealed class SolicitudViajeConfiguracion : IEntityTypeConfiguration<SolicitudViaje>
{
    public void Configure(EntityTypeBuilder<SolicitudViaje> entidad)
    {
        entidad.ToTable("solicitud_viaje",
            tabla => tabla.HasCheckConstraint("ck_solicitud_viaje_pasajeros", "cantidad_pasajeros > 0"));
        entidad.HasKey(solicitud => solicitud.Id);
        entidad.Property(solicitud => solicitud.Id).ValueGeneratedNever();
        entidad.Property(solicitud => solicitud.PasajeroId);
        entidad.Property(solicitud => solicitud.EmpresaClienteId);
        entidad.Property(solicitud => solicitud.ItinerarioId);
        entidad.Property(solicitud => solicitud.DireccionOrigen).HasMaxLength(200).IsRequired();
        entidad.Property(solicitud => solicitud.DireccionDestino).HasMaxLength(200).IsRequired();
        entidad.Property(solicitud => solicitud.FechaHoraServicio);
        entidad.Property(solicitud => solicitud.DuracionEstimada);
        entidad.Property(solicitud => solicitud.CantidadPasajeros);
        entidad.Property(solicitud => solicitud.Urgente);
        entidad.Property(solicitud => solicitud.EsPersonal);
        entidad.Property(solicitud => solicitud.Canal).ComoTexto();
        entidad.Property(solicitud => solicitud.Estado).ComoTexto();
        entidad.Property(solicitud => solicitud.RegistradaEn);
        entidad.ConVersion();

        entidad.HasIndex(solicitud => new { solicitud.Estado, solicitud.FechaHoraServicio });

        entidad.HasOne<Pasajero>().WithMany()
            .HasForeignKey(solicitud => solicitud.PasajeroId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<EmpresaCliente>().WithMany()
            .HasForeignKey(solicitud => solicitud.EmpresaClienteId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Itinerario>().WithMany()
            .HasForeignKey(solicitud => solicitud.ItinerarioId).OnDelete(DeleteBehavior.Restrict);

        entidad.HasMany(solicitud => solicitud.Historial).WithOne()
            .HasForeignKey(historial => historial.SolicitudId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(solicitud => solicitud.Historial).UsePropertyAccessMode(PropertyAccessMode.Field);

        entidad.HasOne(solicitud => solicitud.Cancelacion).WithOne()
            .HasForeignKey<CancelacionSolicitud>(cancelacion => cancelacion.SolicitudId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EstadoSolicitudHistorialConfiguracion : IEntityTypeConfiguration<EstadoSolicitudHistorial>
{
    public void Configure(EntityTypeBuilder<EstadoSolicitudHistorial> entidad)
    {
        entidad.ToTable("estado_solicitud_historial");
        entidad.HasKey(historial => historial.Id);
        entidad.Property(historial => historial.Id).ValueGeneratedNever();
        entidad.Property(historial => historial.SolicitudId);
        entidad.Property(historial => historial.EstadoAnterior).HasConversion<string>().HasMaxLength(Mapeo.LargoEnum);
        entidad.Property(historial => historial.EstadoNuevo).ComoTexto();
        entidad.Property(historial => historial.FechaHora);
        entidad.Property(historial => historial.ActorId);
        entidad.Property(historial => historial.Motivo);

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(historial => historial.ActorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CancelacionSolicitudConfiguracion : IEntityTypeConfiguration<CancelacionSolicitud>
{
    public void Configure(EntityTypeBuilder<CancelacionSolicitud> entidad)
    {
        entidad.ToTable("cancelacion_solicitud");
        entidad.HasKey(cancelacion => cancelacion.Id);
        entidad.Property(cancelacion => cancelacion.Id).ValueGeneratedNever();
        entidad.Property(cancelacion => cancelacion.SolicitudId);
        entidad.Property(cancelacion => cancelacion.Motivo).IsRequired();
        entidad.Property(cancelacion => cancelacion.FechaHora);
        entidad.Property(cancelacion => cancelacion.CanceladaPorId);

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(cancelacion => cancelacion.CanceladaPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Una solicitud tiene a lo sumo una asignación vigente (índice único parcial) y un conductor o vehículo no puede
/// tener dos asignaciones vigentes solapadas (restricciones EXCLUDE en la migración), ADR-0007.
/// </summary>
internal sealed class AsignacionConfiguracion : IEntityTypeConfiguration<Asignacion>
{
    public static readonly string FiltroVigentes =
        $"estado IN ({Mapeo.ListaSql(EstadoAsignacion.Asignada, EstadoAsignacion.Aceptada, EstadoAsignacion.EnCurso)})";

    public void Configure(EntityTypeBuilder<Asignacion> entidad)
    {
        entidad.ToTable("asignacion", tabla => tabla.HasCheckConstraint("ck_asignacion_franja", "franja_fin > franja_inicio"));
        entidad.HasKey(asignacion => asignacion.Id);
        entidad.Property(asignacion => asignacion.Id).ValueGeneratedNever();
        entidad.Property(asignacion => asignacion.SolicitudId);
        entidad.Property(asignacion => asignacion.ConductorId);
        entidad.Property(asignacion => asignacion.VehiculoId);
        entidad.ComplexProperty(asignacion => asignacion.Franja, franja =>
        {
            franja.Property(valor => valor.Inicio).HasColumnName("franja_inicio");
            franja.Property(valor => valor.Fin).HasColumnName("franja_fin");
        });
        entidad.Property(asignacion => asignacion.Origen).ComoTexto();
        entidad.Property(asignacion => asignacion.Estado).ComoTexto();
        entidad.Property(asignacion => asignacion.AsignadaEn);
        // Referencia por identidad al decision log del motor; su tabla llega en la Fase 5.
        entidad.Property(asignacion => asignacion.DecisionId);
        entidad.Property(asignacion => asignacion.AsignacionReemplazadaId);
        entidad.Property(asignacion => asignacion.MotivoCierre).HasMaxLength(300);
        entidad.ConVersion();

        entidad.HasIndex(asignacion => asignacion.SolicitudId)
            .IsUnique()
            .HasFilter(FiltroVigentes)
            .HasDatabaseName("ux_asignacion_solicitud_vigente");

        entidad.HasOne<SolicitudViaje>().WithMany()
            .HasForeignKey(asignacion => asignacion.SolicitudId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Conductor>().WithMany()
            .HasForeignKey(asignacion => asignacion.ConductorId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Vehiculo>().WithMany()
            .HasForeignKey(asignacion => asignacion.VehiculoId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Asignacion>().WithMany()
            .HasForeignKey(asignacion => asignacion.AsignacionReemplazadaId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Agregado Viaje: incluye sus paradas e incidentes.</summary>
internal sealed class ViajeConfiguracion : IEntityTypeConfiguration<Viaje>
{
    public void Configure(EntityTypeBuilder<Viaje> entidad)
    {
        entidad.ToTable("viaje");
        entidad.HasKey(viaje => viaje.Id);
        entidad.Property(viaje => viaje.Id).ValueGeneratedNever();
        entidad.Property(viaje => viaje.AsignacionId);
        entidad.Property(viaje => viaje.Estado).ComoTexto();
        entidad.Property(viaje => viaje.FechaHoraInicio);
        entidad.Property(viaje => viaje.FechaHoraFin);
        entidad.ConVersion();

        entidad.HasOne<Asignacion>().WithOne()
            .HasForeignKey<Viaje>(viaje => viaje.AsignacionId).OnDelete(DeleteBehavior.Restrict);

        entidad.HasMany(viaje => viaje.Paradas).WithOne()
            .HasForeignKey(parada => parada.ViajeId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(viaje => viaje.Paradas).UsePropertyAccessMode(PropertyAccessMode.Field);

        entidad.HasMany(viaje => viaje.Incidentes).WithOne()
            .HasForeignKey(incidente => incidente.ViajeId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(viaje => viaje.Incidentes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ParadaConfiguracion : IEntityTypeConfiguration<Parada>
{
    public void Configure(EntityTypeBuilder<Parada> entidad)
    {
        entidad.ToTable("parada");
        entidad.HasKey(parada => parada.Id);
        entidad.Property(parada => parada.Id).ValueGeneratedNever();
        entidad.Property(parada => parada.ViajeId);
        entidad.Property(parada => parada.Orden);
        entidad.Property(parada => parada.Direccion).HasMaxLength(200).IsRequired();
        entidad.Property(parada => parada.HoraEstimada);
        entidad.Property(parada => parada.HoraReal);
        entidad.HasIndex(parada => new { parada.ViajeId, parada.Orden }).IsUnique();
    }
}

internal sealed class IncidenteConfiguracion : IEntityTypeConfiguration<Incidente>
{
    public void Configure(EntityTypeBuilder<Incidente> entidad)
    {
        entidad.ToTable("incidente");
        entidad.HasKey(incidente => incidente.Id);
        entidad.Property(incidente => incidente.Id).ValueGeneratedNever();
        entidad.Property(incidente => incidente.ViajeId);
        entidad.Property(incidente => incidente.Tipo).ComoTexto();
        entidad.Property(incidente => incidente.Descripcion).IsRequired();
        entidad.Property(incidente => incidente.FechaHora);
        entidad.Property(incidente => incidente.ReportadoPorId);

        entidad.HasOne<Usuario>().WithMany()
            .HasForeignKey(incidente => incidente.ReportadoPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CalificacionConfiguracion : IEntityTypeConfiguration<Calificacion>
{
    public void Configure(EntityTypeBuilder<Calificacion> entidad)
    {
        entidad.ToTable("calificacion",
            tabla => tabla.HasCheckConstraint("ck_calificacion_puntaje", "puntaje BETWEEN 1 AND 5"));
        entidad.HasKey(calificacion => calificacion.Id);
        entidad.Property(calificacion => calificacion.Id).ValueGeneratedNever();
        entidad.Property(calificacion => calificacion.ViajeId);
        entidad.Property(calificacion => calificacion.PasajeroId);
        entidad.Property(calificacion => calificacion.Puntaje);
        entidad.Property(calificacion => calificacion.Comentario);
        entidad.Property(calificacion => calificacion.FechaHora);

        // Un pasajero califica una sola vez cada viaje (PAS-AUD-03).
        entidad.HasIndex(calificacion => new { calificacion.ViajeId, calificacion.PasajeroId }).IsUnique();
        entidad.HasOne<Viaje>().WithMany()
            .HasForeignKey(calificacion => calificacion.ViajeId).OnDelete(DeleteBehavior.Restrict);
        entidad.HasOne<Pasajero>().WithMany()
            .HasForeignKey(calificacion => calificacion.PasajeroId).OnDelete(DeleteBehavior.Restrict);
    }
}
