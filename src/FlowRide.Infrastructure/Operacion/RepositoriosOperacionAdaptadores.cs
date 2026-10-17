using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Operacion;

// Adaptadores de salida de los recursos operativos sobre el Silo. Crear y modificar dejan el cambio pendiente: lo
// confirma la unidad de trabajo junto con su evento de auditoría. Los campos cifrados se comparan por valor exacto:
// EF cifra el parámetro con la misma clave (cifrado determinista, ADR-0014).

internal static class Consultas
{
    /// <summary>Patrón ILIKE con los comodines del texto escapados.</summary>
    public static string Contiene(string texto) =>
        "%" + texto.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal) + "%";

    public static async Task<PaginaResultado<T>> PaginarAsync<T>(IQueryable<T> consulta, Paginacion paginacion, CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.Skip(paginacion.Salto).Take(paginacion.Tamano).ToListAsync(ct);
        return new PaginaResultado<T>(elementos, total, paginacion);
    }

    public static void Modificar<T>(FlowRideDbContext contexto, T entidad) where T : class
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (contexto.Entry(entidad).State == EntityState.Detached)
            contexto.Update(entidad);
    }
}

public sealed class EmpresaClienteRepositorioAdaptador(FlowRideDbContext contexto) : IEmpresaClienteRepositorio
{
    public Task CrearAsync(EmpresaCliente entidad, CancellationToken ct)
    {
        contexto.EmpresasCliente.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, EmpresaCliente entidad, CancellationToken ct)
    {
        Consultas.Modificar(contexto, entidad);
        return Task.CompletedTask;
    }

    public Task<EmpresaCliente?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.EmpresasCliente.SingleOrDefaultAsync(empresa => empresa.Id == id, ct);

    public async Task<IReadOnlyList<EmpresaCliente>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.EmpresasCliente.OrderBy(empresa => empresa.Nombre).ToListAsync(ct);

    public Task<EmpresaCliente?> ConsultarPorNitAsync(string nit, CancellationToken ct) =>
        contexto.EmpresasCliente.SingleOrDefaultAsync(empresa => empresa.Nit == nit, ct);

    public Task<PaginaResultado<EmpresaCliente>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct)
    {
        var consulta = contexto.EmpresasCliente.AsNoTracking();
        if (filtro.Texto is { } texto)
            consulta = consulta.Where(e => EF.Functions.ILike(e.Nombre, Consultas.Contiene(texto)) || e.Nit == texto);
        if (filtro.Activo is { } activa)
            consulta = consulta.Where(e => e.Activa == activa);
        return Consultas.PaginarAsync(consulta.OrderBy(e => e.Nombre).ThenBy(e => e.Id), paginacion, ct);
    }
}

public sealed class PasajeroRepositorioAdaptador(FlowRideDbContext contexto) : IPasajeroRepositorio
{
    public Task CrearAsync(Pasajero entidad, CancellationToken ct)
    {
        contexto.Pasajeros.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, Pasajero entidad, CancellationToken ct)
    {
        Consultas.Modificar(contexto, entidad);
        return Task.CompletedTask;
    }

    public Task<Pasajero?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Pasajeros.SingleOrDefaultAsync(pasajero => pasajero.Id == id, ct);

    public async Task<IReadOnlyList<Pasajero>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Pasajeros.OrderBy(pasajero => pasajero.Nombre).ToListAsync(ct);

    public Task<Pasajero?> ConsultarPorCodigoAsync(Guid empresaClienteId, string codigoInterno, CancellationToken ct) =>
        contexto.Pasajeros.SingleOrDefaultAsync(p => p.EmpresaClienteId == empresaClienteId && p.CodigoInterno == codigoInterno, ct);

    public Task<PaginaResultado<Pasajero>> BuscarAsync(FiltroPasajeros filtro, Paginacion paginacion, CancellationToken ct)
    {
        var consulta = contexto.Pasajeros.AsNoTracking();
        if (filtro.Texto is { } texto)
            consulta = consulta.Where(p => EF.Functions.ILike(p.Nombre, Consultas.Contiene(texto))
                                           || p.CodigoInterno == texto || p.TelefonoWhatsApp == texto);
        if (filtro.EmpresaClienteId is { } empresaId)
            consulta = consulta.Where(p => p.EmpresaClienteId == empresaId);
        if (filtro.Activo is { } activo)
            consulta = consulta.Where(p => p.Activo == activo);
        return Consultas.PaginarAsync(consulta.OrderBy(p => p.Nombre).ThenBy(p => p.Id), paginacion, ct);
    }
}

public sealed class ConductorRepositorioAdaptador(FlowRideDbContext contexto) : IConductorRepositorio
{
    public Task CrearAsync(Conductor entidad, CancellationToken ct)
    {
        contexto.Conductores.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, Conductor entidad, CancellationToken ct)
    {
        Consultas.Modificar(contexto, entidad);
        return Task.CompletedTask;
    }

    /// <summary>El agregado completo: con sus turnos y documentos.</summary>
    public Task<Conductor?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Conductores
            .Include(conductor => conductor.Turnos)
            .Include(conductor => conductor.Documentos)
            .AsSplitQuery()
            .SingleOrDefaultAsync(conductor => conductor.Id == id, ct);

    public async Task<IReadOnlyList<Conductor>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Conductores.OrderBy(conductor => conductor.Nombre).ToListAsync(ct);

    public Task<Conductor?> ConsultarPorLicenciaAsync(string numeroLicencia, CancellationToken ct) =>
        contexto.Conductores.AsNoTracking().SingleOrDefaultAsync(conductor => conductor.NumeroLicencia == numeroLicencia, ct);

    public Task<PaginaResultado<Conductor>> BuscarAsync(FiltroConductores filtro, Paginacion paginacion, CancellationToken ct)
    {
        var consulta = contexto.Conductores.AsNoTracking();
        if (filtro.Texto is { } texto)
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nombre, Consultas.Contiene(texto))
                                           || c.NumeroLicencia == texto || c.TelefonoWhatsApp == texto);
        if (filtro.Estado is { } estado)
            consulta = consulta.Where(c => c.Estado == estado);
        return Consultas.PaginarAsync(consulta.OrderBy(c => c.Nombre).ThenBy(c => c.Id), paginacion, ct);
    }

    public async Task<IReadOnlyList<Conductor>> ConsultarConTurnoQueCubreAsync(FranjaHoraria franja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(franja);
        return await contexto.Conductores.AsNoTracking()
            .Where(c => c.Activo && c.Estado == EstadoConductor.Activo
                        && c.Turnos.Any(t => (t.Estado == EstadoTurno.Programado || t.Estado == EstadoTurno.Activo)
                                             && t.Franja.Inicio <= franja.Inicio && t.Franja.Fin >= franja.Fin))
            .Include(c => c.Turnos)
            .ToListAsync(ct);
    }
}

public sealed class VehiculoRepositorioAdaptador(FlowRideDbContext contexto) : IVehiculoRepositorio
{
    public Task CrearAsync(Vehiculo entidad, CancellationToken ct)
    {
        contexto.Vehiculos.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, Vehiculo entidad, CancellationToken ct)
    {
        Consultas.Modificar(contexto, entidad);
        return Task.CompletedTask;
    }

    public Task<Vehiculo?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Vehiculos.SingleOrDefaultAsync(vehiculo => vehiculo.Id == id, ct);

    public async Task<IReadOnlyList<Vehiculo>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Vehiculos.OrderBy(vehiculo => vehiculo.Placa).ToListAsync(ct);

    public Task<Vehiculo?> ConsultarPorPlacaAsync(string placa, CancellationToken ct) =>
        contexto.Vehiculos.AsNoTracking().SingleOrDefaultAsync(vehiculo => vehiculo.Placa == placa, ct);

    public Task<Vehiculo?> ConsultarPorConductorHabitualAsync(Guid conductorId, CancellationToken ct) =>
        contexto.Vehiculos.AsNoTracking().SingleOrDefaultAsync(vehiculo => vehiculo.ConductorHabitualId == conductorId, ct);

    public Task<PaginaResultado<Vehiculo>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct)
    {
        var consulta = contexto.Vehiculos.AsNoTracking();
        if (filtro.Texto is { } texto)
            consulta = consulta.Where(v => EF.Functions.ILike(v.Placa, Consultas.Contiene(texto))
                                           || EF.Functions.ILike(v.Marca, Consultas.Contiene(texto))
                                           || EF.Functions.ILike(v.Modelo, Consultas.Contiene(texto)));
        if (filtro.Activo is { } activo)
            consulta = consulta.Where(v => v.Activo == activo);
        return Consultas.PaginarAsync(consulta.OrderBy(v => v.Placa), paginacion, ct);
    }
}

public sealed class AsignacionRepositorioAdaptador(FlowRideDbContext contexto) : IAsignacionRepositorio
{
    public async Task<IReadOnlySet<Guid>> ConsultarConductoresOcupadosAsync(FranjaHoraria franja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(franja);
        var ocupados = await contexto.Asignaciones.AsNoTracking()
            .Where(a => (a.Estado == EstadoAsignacion.Asignada || a.Estado == EstadoAsignacion.Aceptada
                         || a.Estado == EstadoAsignacion.EnCurso)
                        && a.Franja.Inicio < franja.Fin && a.Franja.Fin > franja.Inicio)
            .Select(a => a.ConductorId)
            .Distinct()
            .ToListAsync(ct);
        return ocupados.ToHashSet();
    }
}
