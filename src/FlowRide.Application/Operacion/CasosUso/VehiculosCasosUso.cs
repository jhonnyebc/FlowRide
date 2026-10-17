using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;

namespace FlowRide.Application.Operacion.CasosUso;

/// <summary>Alta de un vehículo de la flota (RF-04). La placa es única en el Silo.</summary>
public sealed class RegistrarVehiculoCasoUso(
    VehiculoDominio vehiculoDominio,
    IVehiculoRepositorio vehiculos,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarVehiculoDominio, Vehiculo>
{
    public async Task<Vehiculo> EjecutarAsync(RegistrarVehiculoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        VehiculoDominio.ValidarCapacidad(datos.Capacidad);
        await vehiculoDominio.ValidarPlacaDisponibleAsync(datos.Placa, ct);

        var ahora = reloj.GetUtcNow();
        var vehiculo = new Vehiculo(Guid.CreateVersion7(ahora), datos.Placa, datos.Marca, datos.Modelo, datos.Capacidad,
            datos.VencimientoRtm, datos.VencimientoSoat);
        await vehiculos.CrearAsync(vehiculo, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoVehiculo,
            AccionesAuditoria.Crear, vehiculo.Id, ahora,
            despues: Auditar.Valores(("placa", vehiculo.Placa), ("activo", vehiculo.Activo))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return vehiculo;
    }
}

/// <summary>Datos y documentación (RTM y SOAT) del vehículo.</summary>
public sealed class ActualizarVehiculoCasoUso(
    VehiculoDominio vehiculoDominio,
    IVehiculoRepositorio vehiculos,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarVehiculoDominio, Vehiculo>
{
    public async Task<Vehiculo> EjecutarAsync(ActualizarVehiculoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        VehiculoDominio.ValidarCapacidad(datos.Capacidad);
        var vehiculo = await vehiculoDominio.ValidarExistaAsync(datos.VehiculoId, ct);

        vehiculo.ActualizarDatos(datos.Marca, datos.Modelo, datos.Capacidad);
        vehiculo.RenovarDocumentacion(datos.VencimientoRtm, datos.VencimientoSoat);
        await vehiculos.ModificarAsync(vehiculo.Id, vehiculo, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoVehiculo,
            AccionesAuditoria.Actualizar, vehiculo.Id, reloj.GetUtcNow()), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return vehiculo;
    }
}

public sealed class CambiarEstadoVehiculoCasoUso(
    VehiculoDominio vehiculoDominio,
    IVehiculoRepositorio vehiculos,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarEstadoVehiculoDominio, Vehiculo>
{
    public async Task<Vehiculo> EjecutarAsync(CambiarEstadoVehiculoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var vehiculo = await vehiculoDominio.ValidarExistaAsync(datos.VehiculoId, ct);
        if (vehiculo.Activo == datos.Activo)
            return vehiculo;

        if (datos.Activo) vehiculo.Activar();
        else vehiculo.Desactivar();
        await vehiculos.ModificarAsync(vehiculo.Id, vehiculo, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoVehiculo,
            datos.Activo ? AccionesAuditoria.Activar : AccionesAuditoria.Desactivar, vehiculo.Id, reloj.GetUtcNow(),
            Auditar.Valores(("activo", !datos.Activo)), Auditar.Valores(("activo", datos.Activo))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return vehiculo;
    }
}

/// <summary>Asigna o quita (null) el conductor habitual del vehículo; un conductor es habitual de un solo vehículo.</summary>
public sealed class AsignarConductorHabitualCasoUso(
    VehiculoDominio vehiculoDominio,
    ConductorDominio conductorDominio,
    IVehiculoRepositorio vehiculos,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<AsignarConductorHabitualDominio, Vehiculo>
{
    public async Task<Vehiculo> EjecutarAsync(AsignarConductorHabitualDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var vehiculo = await vehiculoDominio.ValidarExistaAsync(datos.VehiculoId, ct);
        if (datos.ConductorId is { } conductorId)
        {
            await conductorDominio.ValidarExistaAsync(conductorId, ct);
            await vehiculoDominio.ValidarConductorSinVehiculoHabitualAsync(conductorId, vehiculo.Id, ct);
        }
        if (vehiculo.ConductorHabitualId == datos.ConductorId)
            return vehiculo;

        var antes = Auditar.Valores(("conductor_habitual_id", vehiculo.ConductorHabitualId));
        vehiculo.AsignarConductorHabitual(datos.ConductorId);
        await vehiculos.ModificarAsync(vehiculo.Id, vehiculo, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoVehiculo,
            AccionesAuditoria.AsignarConductorHabitual, vehiculo.Id, reloj.GetUtcNow(), antes,
            Auditar.Valores(("conductor_habitual_id", vehiculo.ConductorHabitualId))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return vehiculo;
    }
}

public sealed class BuscarVehiculosCasoUso(IVehiculoRepositorio vehiculos)
    : ICasoUsoConRetorno<BuscarVehiculosDominio, PaginaResultado<Vehiculo>>
{
    public Task<PaginaResultado<Vehiculo>> EjecutarAsync(BuscarVehiculosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return vehiculos.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}
