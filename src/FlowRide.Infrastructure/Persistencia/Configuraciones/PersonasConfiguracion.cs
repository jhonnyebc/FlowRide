using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

internal sealed class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> entidad)
    {
        entidad.ToTable("rol");
        entidad.HasKey(rol => rol.Id);
        entidad.Property(rol => rol.Id).ValueGeneratedNever();
        entidad.Property(rol => rol.Nombre).HasMaxLength(50).IsRequired();
        entidad.Property(rol => rol.Descripcion).HasMaxLength(200).IsRequired();
        entidad.HasIndex(rol => rol.Nombre).IsUnique();

        // Los cuatro roles del sistema llegan con la migración, con Id fijos (RBAC, ADR-0003).
        entidad.HasData(RolesSistema.Todos.Select(rol => new { rol.Id, rol.Nombre, rol.Descripcion }));
    }
}

/// <summary>Herencia por tablas (TPT): ops.usuario, ops.pasajero y ops.conductor comparten la llave.</summary>
internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> entidad)
    {
        entidad.ToTable("usuario");
        entidad.UseTptMappingStrategy();
        entidad.HasKey(usuario => usuario.Id);
        entidad.Property(usuario => usuario.Id).ValueGeneratedNever();
        entidad.Property(usuario => usuario.Nombre).HasMaxLength(150).IsRequired();
        entidad.Property(usuario => usuario.Correo).HasMaxLength(150);
        entidad.Property(usuario => usuario.TelefonoWhatsApp).HasMaxLength(20).IsRequired();
        entidad.Property(usuario => usuario.IdentidadExternaId).HasMaxLength(100);
        entidad.Property(usuario => usuario.RolId);
        entidad.Property(usuario => usuario.Activo);
        entidad.Property(usuario => usuario.CreadoEn);
        entidad.ConVersion();

        entidad.HasIndex(usuario => usuario.Correo).IsUnique();
        entidad.HasIndex(usuario => usuario.TelefonoWhatsApp).IsUnique();
        entidad.HasIndex(usuario => usuario.IdentidadExternaId).IsUnique();
        entidad.HasOne<Rol>().WithMany().HasForeignKey(usuario => usuario.RolId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PasajeroConfiguracion : IEntityTypeConfiguration<Pasajero>
{
    public void Configure(EntityTypeBuilder<Pasajero> entidad)
    {
        entidad.ToTable("pasajero");
        entidad.Property(pasajero => pasajero.CodigoInterno).HasMaxLength(30).IsRequired();
        entidad.Property(pasajero => pasajero.EmpresaClienteId);

        entidad.HasIndex(pasajero => new { pasajero.EmpresaClienteId, pasajero.CodigoInterno }).IsUnique();
        entidad.HasOne<EmpresaCliente>().WithMany()
            .HasForeignKey(pasajero => pasajero.EmpresaClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Agregado Conductor: incluye sus turnos y documentos.</summary>
internal sealed class ConductorConfiguracion : IEntityTypeConfiguration<Conductor>
{
    public void Configure(EntityTypeBuilder<Conductor> entidad)
    {
        entidad.ToTable("conductor");
        entidad.Property(conductor => conductor.NumeroLicencia).HasMaxLength(30).IsRequired();
        entidad.Property(conductor => conductor.CategoriaLicencia).HasMaxLength(5).IsRequired();
        entidad.Property(conductor => conductor.VencimientoLicencia);
        entidad.Property(conductor => conductor.Estado).ComoTexto();
        entidad.Property(conductor => conductor.FechaUltimaAsignacion);

        entidad.HasIndex(conductor => conductor.NumeroLicencia).IsUnique();

        entidad.HasMany(conductor => conductor.Turnos).WithOne()
            .HasForeignKey(turno => turno.ConductorId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(conductor => conductor.Turnos).UsePropertyAccessMode(PropertyAccessMode.Field);

        entidad.HasMany(conductor => conductor.Documentos).WithOne()
            .HasForeignKey(documento => documento.ConductorId).OnDelete(DeleteBehavior.Cascade);
        entidad.Navigation(conductor => conductor.Documentos).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>Un conductor no puede tener dos turnos vigentes solapados: restricción EXCLUDE en la migración.</summary>
internal sealed class TurnoConfiguracion : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> entidad)
    {
        entidad.ToTable("turno", tabla => tabla.HasCheckConstraint("ck_turno_franja", "fin > inicio"));
        entidad.HasKey(turno => turno.Id);
        entidad.Property(turno => turno.Id).ValueGeneratedNever();
        entidad.Property(turno => turno.ConductorId);
        entidad.ComplexProperty(turno => turno.Franja, franja =>
        {
            franja.Property(valor => valor.Inicio).HasColumnName("inicio");
            franja.Property(valor => valor.Fin).HasColumnName("fin");
        });
        entidad.Property(turno => turno.Estado).ComoTexto();
        entidad.Property(turno => turno.MotivoAbandono).HasMaxLength(300);
        entidad.ConVersion();
    }
}

internal sealed class DocumentoConductorConfiguracion : IEntityTypeConfiguration<DocumentoConductor>
{
    public void Configure(EntityTypeBuilder<DocumentoConductor> entidad)
    {
        entidad.ToTable("documento_conductor");
        entidad.HasKey(documento => documento.Id);
        entidad.Property(documento => documento.Id).ValueGeneratedNever();
        entidad.Property(documento => documento.ConductorId);
        entidad.Property(documento => documento.Tipo).ComoTexto();
        entidad.Property(documento => documento.ReferenciaBlob).HasMaxLength(300).IsRequired();
        entidad.Property(documento => documento.FechaVencimiento);
    }
}

internal sealed class VehiculoConfiguracion : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> entidad)
    {
        entidad.ToTable("vehiculo", tabla => tabla.HasCheckConstraint("ck_vehiculo_capacidad", "capacidad > 0"));
        entidad.HasKey(vehiculo => vehiculo.Id);
        entidad.Property(vehiculo => vehiculo.Id).ValueGeneratedNever();
        entidad.Property(vehiculo => vehiculo.Placa).HasMaxLength(10).IsRequired();
        entidad.Property(vehiculo => vehiculo.Marca).HasMaxLength(50).IsRequired();
        entidad.Property(vehiculo => vehiculo.Modelo).HasMaxLength(50).IsRequired();
        entidad.Property(vehiculo => vehiculo.Capacidad);
        entidad.Property(vehiculo => vehiculo.VencimientoRtm);
        entidad.Property(vehiculo => vehiculo.VencimientoSoat);
        entidad.Property(vehiculo => vehiculo.Activo);
        entidad.Property(vehiculo => vehiculo.ConductorHabitualId);
        entidad.ConVersion();

        entidad.HasIndex(vehiculo => vehiculo.Placa).IsUnique();
        entidad.HasIndex(vehiculo => vehiculo.ConductorHabitualId).IsUnique();
        entidad.HasOne<Conductor>().WithMany()
            .HasForeignKey(vehiculo => vehiculo.ConductorHabitualId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class EmpresaClienteConfiguracion : IEntityTypeConfiguration<EmpresaCliente>
{
    public void Configure(EntityTypeBuilder<EmpresaCliente> entidad)
    {
        entidad.ToTable("empresa_cliente");
        entidad.HasKey(empresa => empresa.Id);
        entidad.Property(empresa => empresa.Id).ValueGeneratedNever();
        entidad.Property(empresa => empresa.Nombre).HasMaxLength(150).IsRequired();
        entidad.Property(empresa => empresa.Nit).HasMaxLength(20).IsRequired();
        entidad.Property(empresa => empresa.CorreoContacto).HasMaxLength(150).IsRequired();
        entidad.Property(empresa => empresa.Activa);
        entidad.HasIndex(empresa => empresa.Nit).IsUnique();
    }
}

internal sealed class ItinerarioConfiguracion : IEntityTypeConfiguration<Itinerario>
{
    public void Configure(EntityTypeBuilder<Itinerario> entidad)
    {
        entidad.ToTable("itinerario");
        entidad.HasKey(itinerario => itinerario.Id);
        entidad.Property(itinerario => itinerario.Id).ValueGeneratedNever();
        entidad.Property(itinerario => itinerario.EmpresaClienteId);
        entidad.Property(itinerario => itinerario.CodigoServicio).HasMaxLength(30).IsRequired();
        entidad.Property(itinerario => itinerario.Fecha);
        entidad.Property(itinerario => itinerario.Origen).HasMaxLength(200).IsRequired();
        entidad.Property(itinerario => itinerario.Destino).HasMaxLength(200).IsRequired();
        entidad.Property(itinerario => itinerario.HoraSalida);
        entidad.Property(itinerario => itinerario.HoraLlegada);

        entidad.HasIndex(itinerario => new { itinerario.EmpresaClienteId, itinerario.CodigoServicio, itinerario.Fecha })
            .IsUnique();
        entidad.HasOne<EmpresaCliente>().WithMany()
            .HasForeignKey(itinerario => itinerario.EmpresaClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}
