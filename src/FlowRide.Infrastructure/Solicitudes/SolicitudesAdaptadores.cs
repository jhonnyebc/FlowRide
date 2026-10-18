using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Solicitudes;

/// <summary>Lado de escritura de las solicitudes: el agregado con su historial y su cancelación.</summary>
public sealed class SolicitudRepositorioAdaptador(FlowRideDbContext contexto) : ISolicitudRepositorio
{
    private static readonly EstadoSolicitud[] Activas =
        [EstadoSolicitud.Pendiente, EstadoSolicitud.Asignada, EstadoSolicitud.SinCandidatos, EstadoSolicitud.EnCurso];

    public Task CrearAsync(SolicitudViaje entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Solicitudes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, SolicitudViaje entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (contexto.Entry(entidad).State == EntityState.Detached)
            contexto.Solicitudes.Update(entidad);
        return Task.CompletedTask;
    }

    public Task<SolicitudViaje?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Solicitudes
            .Include(solicitud => solicitud.Historial)
            .Include(solicitud => solicitud.Cancelacion)
            .AsSplitQuery()
            .SingleOrDefaultAsync(solicitud => solicitud.Id == id, ct);

    public async Task<IReadOnlyList<SolicitudViaje>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Solicitudes.OrderBy(solicitud => solicitud.FechaHoraServicio).ToListAsync(ct);

    public Task<bool> ExisteActivaQueSeSolapaAsync(Guid pasajeroId, FranjaHoraria franja, Guid? excluirSolicitudId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(franja);
        return contexto.Solicitudes.AnyAsync(s => s.PasajeroId == pasajeroId
                                                  && s.Id != excluirSolicitudId
                                                  && Activas.Contains(s.Estado)
                                                  && s.FechaHoraServicio < franja.Fin
                                                  && s.FechaHoraServicio + s.DuracionEstimada > franja.Inicio, ct);
    }
}

/// <summary>
/// Lado de lectura (CQRS ligero): proyecciones sin rastreo que unen la solicitud con su pasajero y su empresa. Nunca
/// devuelve entidades del dominio.
/// </summary>
public sealed class ConsultasSolicitudesEf(FlowRideDbContext contexto) : IConsultasSolicitudes
{
    public async Task<SolicitudDetalleLectura?> DetalleAsync(Guid solicitudId, CancellationToken ct)
    {
        var solicitud = await Proyectar(contexto.Solicitudes.AsNoTracking().Where(s => s.Id == solicitudId)).SingleOrDefaultAsync(ct);
        if (solicitud is null)
            return null;

        var historial = await contexto.Set<EstadoSolicitudHistorial>().AsNoTracking()
            .Where(h => h.SolicitudId == solicitudId)
            .OrderBy(h => h.FechaHora).ThenBy(h => h.Id)
            .Select(h => new CambioEstadoLectura(h.EstadoAnterior, h.EstadoNuevo, h.FechaHora, h.ActorId, h.Motivo))
            .ToListAsync(ct);
        return new SolicitudDetalleLectura(solicitud, historial);
    }

    public async Task<PaginaResultado<SolicitudLectura>> BuscarAsync(FiltroSolicitudes filtro, Paginacion paginacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = contexto.Solicitudes.AsNoTracking();
        if (filtro.Estado is { } estado) consulta = consulta.Where(s => s.Estado == estado);
        if (filtro.PasajeroId is { } pasajeroId) consulta = consulta.Where(s => s.PasajeroId == pasajeroId);
        if (filtro.EmpresaClienteId is { } empresaId) consulta = consulta.Where(s => s.EmpresaClienteId == empresaId);
        if (filtro.Desde is { } desde) consulta = consulta.Where(s => s.FechaHoraServicio >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(s => s.FechaHoraServicio < hasta);
        if (filtro.Urgente is { } urgente) consulta = consulta.Where(s => s.Urgente == urgente);

        // La cola de asignación va en el orden en que se atiende: urgentes primero y luego por hora del servicio (RF-13).
        // El historial, de lo más reciente a lo más antiguo.
        consulta = filtro.Estado == EstadoSolicitud.Pendiente
            ? consulta.OrderByDescending(s => s.Urgente).ThenBy(s => s.FechaHoraServicio).ThenBy(s => s.Id)
            : consulta.OrderByDescending(s => s.FechaHoraServicio).ThenBy(s => s.Id);

        var total = await consulta.CountAsync(ct);
        var elementos = await Proyectar(consulta.Skip(paginacion.Salto).Take(paginacion.Tamano)).ToListAsync(ct);
        return new PaginaResultado<SolicitudLectura>(elementos, total, paginacion);
    }

    private IQueryable<SolicitudLectura> Proyectar(IQueryable<SolicitudViaje> solicitudes) =>
        from s in solicitudes
        join p in contexto.Pasajeros on s.PasajeroId equals p.Id
        join e in contexto.EmpresasCliente on s.EmpresaClienteId equals e.Id into empresas
        from e in empresas.DefaultIfEmpty()
        select new SolicitudLectura(s.Id, s.PasajeroId, p.Nombre, s.EmpresaClienteId, e == null ? null : e.Nombre, s.ItinerarioId,
            s.DireccionOrigen, s.DireccionDestino, s.FechaHoraServicio, s.DuracionEstimada, s.CantidadPasajeros, s.Urgente,
            s.EsPersonal, s.Canal, s.Estado, s.RegistradaEn, false);
}

public sealed class ItinerarioRepositorioAdaptador(FlowRideDbContext contexto) : IItinerarioRepositorio
{
    public Task CrearAsync(Itinerario entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Itinerarios.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<Itinerario?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.Itinerarios.AsNoTracking().SingleOrDefaultAsync(itinerario => itinerario.Id == id, ct);

    public async Task<IReadOnlyList<Itinerario>> ConsultarTodosAsync(CancellationToken ct) =>
        await contexto.Itinerarios.AsNoTracking().OrderBy(i => i.Fecha).ThenBy(i => i.HoraSalida).ToListAsync(ct);

    public async Task<IReadOnlySet<DateOnly>> ConsultarFechasOcupadasAsync(
        Guid empresaClienteId, string codigoServicio, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var fechas = await contexto.Itinerarios.AsNoTracking()
            .Where(i => i.EmpresaClienteId == empresaClienteId && i.CodigoServicio == codigoServicio && i.Fecha >= desde && i.Fecha <= hasta)
            .Select(i => i.Fecha)
            .ToListAsync(ct);
        return fechas.ToHashSet();
    }

    public async Task<PaginaResultado<Itinerario>> BuscarAsync(FiltroItinerarios filtro, Paginacion paginacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);
        var consulta = contexto.Itinerarios.AsNoTracking();
        if (filtro.EmpresaClienteId is { } empresaId) consulta = consulta.Where(i => i.EmpresaClienteId == empresaId);
        if (filtro.CodigoServicio is { } codigo) consulta = consulta.Where(i => i.CodigoServicio == codigo);
        if (filtro.Desde is { } desde) consulta = consulta.Where(i => i.Fecha >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(i => i.Fecha <= hasta);
        consulta = consulta.OrderBy(i => i.Fecha).ThenBy(i => i.HoraSalida).ThenBy(i => i.CodigoServicio);

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta.Skip(paginacion.Salto).Take(paginacion.Tamano).ToListAsync(ct);
        return new PaginaResultado<Itinerario>(elementos, total, paginacion);
    }
}
