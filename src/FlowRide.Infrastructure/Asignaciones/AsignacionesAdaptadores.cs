using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Asignaciones.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Asignaciones;

public sealed class ConfiguracionAsignacionRepositorioAdaptador(FlowRideDbContext contexto) : IConfiguracionAsignacionRepositorio
{
    public Task CrearAsync(ConfiguracionAsignacion entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.ConfiguracionesAsignacion.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, ConfiguracionAsignacion entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (contexto.Entry(entidad).State == EntityState.Detached)
            contexto.ConfiguracionesAsignacion.Update(entidad);
        return Task.CompletedTask;
    }

    public Task<ConfiguracionAsignacion?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        ConReglas().SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ConfiguracionAsignacion>> ConsultarTodosAsync(CancellationToken ct) =>
        await ConReglas().AsNoTracking().OrderByDescending(c => c.Version).ToListAsync(ct);

    public Task<ConfiguracionAsignacion?> ConsultarActivaAsync(CancellationToken ct) =>
        ConReglas().SingleOrDefaultAsync(c => c.Estado == EstadoConfiguracion.Activa, ct);

    public Task<ConfiguracionAsignacion?> ConsultarPorVersionAsync(int version, CancellationToken ct) =>
        ConReglas().SingleOrDefaultAsync(c => c.Version == version, ct);

    public async Task<int> UltimaVersionAsync(CancellationToken ct) =>
        await contexto.ConfiguracionesAsignacion.MaxAsync(c => (int?)c.Version, ct) ?? 0;

    private IQueryable<ConfiguracionAsignacion> ConReglas() => contexto.ConfiguracionesAsignacion.Include(c => c.Reglas);
}

public sealed class DecisionAsignacionRepositorioAdaptador(FlowRideDbContext contexto) : IDecisionAsignacionRepositorio
{
    public Task CrearAsync(DecisionAsignacion entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.DecisionesAsignacion.Add(entidad);
        return Task.CompletedTask;
    }
}

/// <summary>Lado de lectura (CQRS ligero) de las asignaciones y del decision log.</summary>
public sealed class ConsultasAsignacionesEf(FlowRideDbContext contexto) : IConsultasAsignaciones
{
    public async Task<PaginaResultado<AsignacionLectura>> BuscarAsync(FiltroAsignaciones filtro, Paginacion paginacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = contexto.Asignaciones.AsNoTracking();
        if (filtro.ConductorId is { } conductorId) consulta = consulta.Where(a => a.ConductorId == conductorId);
        if (filtro.SolicitudId is { } solicitudId) consulta = consulta.Where(a => a.SolicitudId == solicitudId);
        if (filtro.Estado is { } estado) consulta = consulta.Where(a => a.Estado == estado);
        if (filtro.Desde is { } desde) consulta = consulta.Where(a => a.Franja.Inicio >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(a => a.Franja.Inicio < hasta);

        var proyeccion =
            from a in consulta
            join c in contexto.Conductores on a.ConductorId equals c.Id
            join v in contexto.Vehiculos on a.VehiculoId equals v.Id
            orderby a.Franja.Inicio descending, a.AsignadaEn descending, a.Id
            select new AsignacionLectura(a.Id, a.SolicitudId, a.ConductorId, c.Nombre, a.VehiculoId, v.Placa, a.Franja.Inicio,
                a.Franja.Fin, a.Estado, a.Origen, a.AsignadaEn, a.MotivoCierre, a.AsignacionReemplazadaId);

        var total = await consulta.CountAsync(ct);
        var elementos = await proyeccion.Skip(paginacion.Salto).Take(paginacion.Tamano).ToListAsync(ct);
        return new PaginaResultado<AsignacionLectura>(elementos, total, paginacion);
    }

    public async Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct)
    {
        var decisiones = await contexto.DecisionesAsignacion.AsNoTracking()
            .Where(d => d.SolicitudId == solicitudId)
            .Include(d => d.Evaluaciones).ThenInclude(e => e.Resultados)
            .AsSplitQuery()
            .OrderByDescending(d => d.EvaluadaEn).ThenByDescending(d => d.Id)
            .ToListAsync(ct);

        return decisiones.Select(d => new DecisionLectura(d.Id, d.SolicitudId, d.VersionConfiguracion, d.Origen, d.EvaluadaEn,
                d.Resultado.ToString(), d.ConductorSeleccionadoId, d.VehiculoSeleccionadoId, d.Motivo, d.Desempate,
                d.Evaluaciones
                    .OrderBy(e => e.PosicionRanking ?? int.MaxValue).ThenBy(e => e.ConductorId).ThenBy(e => e.VehiculoId)
                    .Select(e => new EvaluacionLectura(e.ConductorId, e.VehiculoId, e.TurnoId, e.Elegible, e.MotivoDescarte, e.Prioridad,
                        e.PuntajeTotal, e.PosicionRanking,
                        e.Resultados.OrderBy(r => r.Tipo).ThenBy(r => r.Orden)
                            .Select(r => new ResultadoReglaLectura(r.CodigoRegla, r.VersionRegla, r.Tipo.ToString(), r.Orden, r.Aprobado,
                                r.Valor, r.Peso, r.ValorPonderado, r.Motivo)).ToList()))
                    .ToList()))
            .ToList();
    }
}
