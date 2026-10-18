using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Viajes.GestionViajes;
using FlowRide.Domain.Viajes.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Viajes;

public sealed class ViajeRepositorioAdaptador(FlowRideDbContext contexto) : IViajeRepositorio
{
    public Task CrearAsync(Viaje entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Viajes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<Viaje?> ConsultarPorIdAsync(Guid viajeId, CancellationToken ct) =>
        Completos().SingleOrDefaultAsync(viaje => viaje.Id == viajeId, ct);

    public Task<Viaje?> ConsultarPorAsignacionAsync(Guid asignacionId, CancellationToken ct) =>
        Completos().SingleOrDefaultAsync(viaje => viaje.AsignacionId == asignacionId, ct);

    private IQueryable<Viaje> Completos() => contexto.Viajes.Include(viaje => viaje.Paradas).Include(viaje => viaje.Incidentes);
}

public sealed class CalificacionRepositorioAdaptador(FlowRideDbContext contexto) : ICalificacionRepositorio
{
    public Task CrearAsync(Calificacion entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Calificaciones.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteAsync(Guid viajeId, Guid pasajeroId, CancellationToken ct) =>
        contexto.Calificaciones.AnyAsync(c => c.ViajeId == viajeId && c.PasajeroId == pasajeroId, ct);
}

/// <summary>Lado de lectura (CQRS ligero) de los viajes: historial, monitoreo y trazabilidad.</summary>
public sealed class ConsultasViajesEf(FlowRideDbContext contexto) : IConsultasViajes
{
    // Acciones auditadas que ya cuentan otras fuentes de la línea de tiempo (la entidad tiene la hora real).
    private static readonly string[] AccionesRepetidas =
    [
        AccionesAuditoria.De(AccionesAuditoria.RecursoViaje, AccionesAuditoria.RegistrarLlegada),
        AccionesAuditoria.De(AccionesAuditoria.RecursoViaje, AccionesAuditoria.ReportarIncidente)
    ];

    private static readonly Dictionary<string, string> DescripcionAcciones = new(StringComparer.Ordinal)
    {
        [AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.AsignarManual)] = "Asignación manual del Operador logístico.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Reasignar)] = "Reasignación manual del Operador logístico.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Aceptar)] = "El conductor aceptó la asignación.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Rechazar)] = "El conductor rechazó la asignación.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Vencer)] = "La asignación venció sin respuesta del conductor.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoViaje, AccionesAuditoria.Iniciar)] = "Viaje iniciado.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoViaje, AccionesAuditoria.Finalizar)] = "Viaje finalizado.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoViaje, AccionesAuditoria.RegistrarParada)] = "Parada registrada.",
        [AccionesAuditoria.De(AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.Modificar)] = "Solicitud modificada."
    };

    public async Task<PaginaResultado<ViajeLectura>> BuscarAsync(FiltroViajes filtro, Paginacion paginacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentNullException.ThrowIfNull(paginacion);

        var consulta = Filas();
        if (filtro.Estado is { } estado) consulta = consulta.Where(v => v.Estado == estado);
        if (filtro.ConductorId is { } conductorId) consulta = consulta.Where(v => v.ConductorId == conductorId);
        if (filtro.PasajeroId is { } pasajeroId) consulta = consulta.Where(v => v.PasajeroId == pasajeroId);
        if (filtro.EmpresaClienteId is { } empresaId) consulta = consulta.Where(v => v.EmpresaClienteId == empresaId);
        if (filtro.Desde is { } desde) consulta = consulta.Where(v => v.InicioProgramado >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(v => v.InicioProgramado < hasta);

        var total = await consulta.CountAsync(ct);
        var elementos = await Lectura(consulta.OrderByDescending(v => v.InicioProgramado).ThenBy(v => v.Id)
            .Skip(paginacion.Salto).Take(paginacion.Tamano)).ToListAsync(ct);
        return new PaginaResultado<ViajeLectura>(elementos, total, paginacion);
    }

    public async Task<ViajeDetalleLectura?> DetalleAsync(Guid viajeId, CancellationToken ct)
    {
        if (await Lectura(Filas().Where(v => v.Id == viajeId)).SingleOrDefaultAsync(ct) is not { } viaje)
            return null;

        var paradas = await contexto.Set<Parada>().AsNoTracking().Where(p => p.ViajeId == viajeId).OrderBy(p => p.Orden)
            .Select(p => new ParadaLectura(p.Id, p.Orden, p.Direccion, p.HoraEstimada, p.HoraReal)).ToListAsync(ct);
        var incidentes = await contexto.Set<Incidente>().AsNoTracking().Where(i => i.ViajeId == viajeId).OrderBy(i => i.FechaHora)
            .Select(i => new IncidenteLectura(i.Id, i.Tipo, i.Descripcion, i.FechaHora, i.ReportadoPorId)).ToListAsync(ct);
        var calificacion = await contexto.Calificaciones.AsNoTracking().Where(c => c.ViajeId == viajeId)
            .Select(c => new CalificacionLectura(c.Id, c.Puntaje, c.Comentario, c.FechaHora)).FirstOrDefaultAsync(ct);
        return new ViajeDetalleLectura(viaje, paradas, incidentes, calificacion);
    }

    public async Task<IReadOnlyList<ViajeLectura>> ActivosAsync(DateTimeOffset programadosHasta, CancellationToken ct) =>
        await Lectura(Filas()
                .Where(v => v.Estado == EstadoViaje.EnCurso || (v.Estado == EstadoViaje.Programado && v.InicioProgramado < programadosHasta))
                .OrderBy(v => v.InicioProgramado).ThenBy(v => v.Id))
            .ToListAsync(ct);

    public async Task<TrazabilidadLectura?> TrazabilidadAsync(Guid solicitudId, CancellationToken ct)
    {
        var solicitud = await contexto.Solicitudes.AsNoTracking().Include(s => s.Historial)
            .SingleOrDefaultAsync(s => s.Id == solicitudId, ct);
        if (solicitud is null)
            return null;

        var eventos = new List<EventoTrazabilidad>();
        eventos.AddRange(solicitud.Historial.Select(h => new EventoTrazabilidad(h.FechaHora, "solicitud",
            h.EstadoAnterior is null ? $"Solicitud registrada ({h.EstadoNuevo})." : $"Solicitud: {h.EstadoAnterior} → {h.EstadoNuevo}.",
            h.ActorId, h.Motivo, solicitud.Id)));

        var decisiones = await contexto.DecisionesAsignacion.AsNoTracking().Where(d => d.SolicitudId == solicitudId)
            .Select(d => new { d.Id, d.EvaluadaEn, d.Origen, d.VersionConfiguracion, d.Resultado, d.Motivo }).ToListAsync(ct);
        eventos.AddRange(decisiones.Select(d => new EventoTrazabilidad(d.EvaluadaEn, "decision",
            $"Motor de asignación ({d.Origen}, configuración v{d.VersionConfiguracion}): {d.Resultado}.", null, d.Motivo, d.Id)));

        var asignaciones = await (
                from a in contexto.Asignaciones.AsNoTracking()
                join c in contexto.Conductores on a.ConductorId equals c.Id
                join v in contexto.Vehiculos on a.VehiculoId equals v.Id
                where a.SolicitudId == solicitudId
                select new { a.Id, a.AsignadaEn, a.Origen, Conductor = c.Nombre, v.Placa })
            .ToListAsync(ct);
        eventos.AddRange(asignaciones.Where(a => a.Origen == OrigenAsignacion.Automatica).Select(a => new EventoTrazabilidad(a.AsignadaEn,
            "asignacion", $"Asignación automática: {a.Conductor} con el vehículo {a.Placa}.", null, null, a.Id)));

        var asignacionIds = asignaciones.Select(a => a.Id).ToList();
        var viajes = await contexto.Viajes.AsNoTracking().Include(v => v.Paradas).Include(v => v.Incidentes)
            .Where(v => asignacionIds.Contains(v.AsignacionId)).ToListAsync(ct);
        foreach (var viaje in viajes)
        {
            eventos.AddRange(viaje.Paradas.Where(p => p.HoraReal is not null).Select(p => new EventoTrazabilidad(p.HoraReal!.Value, "parada",
                $"Llegada a la parada {p.Orden}.", null, null, p.Id)));
            eventos.AddRange(viaje.Incidentes.Select(i => new EventoTrazabilidad(i.FechaHora, "incidente",
                $"Incidente ({i.Tipo}): {i.Descripcion}", i.ReportadoPorId, null, i.Id)));
        }

        var viajeIds = viajes.Select(v => v.Id).ToList();
        var calificaciones = await contexto.Calificaciones.AsNoTracking().Where(c => viajeIds.Contains(c.ViajeId)).ToListAsync(ct);
        eventos.AddRange(calificaciones.Select(c => new EventoTrazabilidad(c.FechaHora, "calificacion",
            $"Calificación del pasajero: {c.Puntaje}/{Calificacion.PuntajeMaximo}.", c.PasajeroId, c.Comentario, c.Id)));

        var recursosAsignacion = asignacionIds.Select(id => id.ToString()).ToList();
        var recursosViaje = viajeIds.Select(id => id.ToString()).ToList();
        var recursoSolicitud = solicitudId.ToString();
        var accionModificar = AccionesAuditoria.De(AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.Modificar);
        var auditados = await contexto.EventosAuditoria.AsNoTracking()
            .Where(e => (e.Recurso == AccionesAuditoria.RecursoAsignacion && recursosAsignacion.Contains(e.RecursoId))
                        || (e.Recurso == AccionesAuditoria.RecursoViaje && recursosViaje.Contains(e.RecursoId))
                        || (e.Recurso == AccionesAuditoria.RecursoSolicitud && e.RecursoId == recursoSolicitud && e.Accion == accionModificar))
            .Where(e => !AccionesRepetidas.Contains(e.Accion))
            .Select(e => new { e.FechaHora, e.Accion, e.ActorId, e.Motivo, e.RecursoId })
            .ToListAsync(ct);
        eventos.AddRange(auditados.Select(e => new EventoTrazabilidad(e.FechaHora, e.Accion,
            DescripcionAcciones.GetValueOrDefault(e.Accion, e.Accion), e.ActorId, e.Motivo,
            Guid.TryParse(e.RecursoId, out var referencia) ? referencia : null)));

        // Orden estable: por hora y, a la misma hora, en el orden en que se armó (solicitud, motor, asignación, viaje…).
        var ordenados = eventos.Select((evento, indice) => (evento, indice))
            .OrderBy(par => par.evento.FechaHora).ThenBy(par => par.indice)
            .Select(par => par.evento).ToList();
        return new TrazabilidadLectura(solicitud.Id, solicitud.Estado, ordenados);
    }

    // Filas con inicializadores (traducibles en Where/OrderBy); el registro de lectura se arma al final.
    private IQueryable<FilaViaje> Filas() =>
        from v in contexto.Viajes.AsNoTracking()
        join a in contexto.Asignaciones on v.AsignacionId equals a.Id
        join s in contexto.Solicitudes on a.SolicitudId equals s.Id
        join p in contexto.Pasajeros on s.PasajeroId equals p.Id
        join c in contexto.Conductores on a.ConductorId equals c.Id
        join ve in contexto.Vehiculos on a.VehiculoId equals ve.Id
        join e in contexto.EmpresasCliente on s.EmpresaClienteId equals e.Id into empresas
        from e in empresas.DefaultIfEmpty()
        select new FilaViaje
        {
            Id = v.Id,
            AsignacionId = v.AsignacionId,
            SolicitudId = s.Id,
            Estado = v.Estado,
            ConductorId = c.Id,
            ConductorNombre = c.Nombre,
            VehiculoId = ve.Id,
            Placa = ve.Placa,
            PasajeroId = p.Id,
            PasajeroNombre = p.Nombre,
            EmpresaClienteId = s.EmpresaClienteId,
            EmpresaClienteNombre = e == null ? null : e.Nombre,
            DireccionOrigen = s.DireccionOrigen,
            DireccionDestino = s.DireccionDestino,
            Urgente = s.Urgente,
            EsPersonal = s.EsPersonal,
            InicioProgramado = a.Franja.Inicio,
            FinProgramado = a.Franja.Fin,
            FechaHoraInicio = v.FechaHoraInicio,
            FechaHoraFin = v.FechaHoraFin,
            Incidentes = v.Incidentes.Count,
            Puntaje = contexto.Calificaciones.Where(cal => cal.ViajeId == v.Id).Select(cal => (int?)cal.Puntaje).FirstOrDefault()
        };

    private static IQueryable<ViajeLectura> Lectura(IQueryable<FilaViaje> filas) =>
        filas.Select(f => new ViajeLectura(f.Id, f.AsignacionId, f.SolicitudId, f.Estado, f.ConductorId, f.ConductorNombre, f.VehiculoId,
            f.Placa, f.PasajeroId, f.PasajeroNombre, f.EmpresaClienteId, f.EmpresaClienteNombre, f.DireccionOrigen, f.DireccionDestino,
            f.Urgente, f.EsPersonal, f.InicioProgramado, f.FinProgramado, f.FechaHoraInicio, f.FechaHoraFin, f.Incidentes, f.Puntaje, false));

    private sealed class FilaViaje
    {
        public Guid Id { get; init; }
        public Guid AsignacionId { get; init; }
        public Guid SolicitudId { get; init; }
        public EstadoViaje Estado { get; init; }
        public Guid ConductorId { get; init; }
        public string ConductorNombre { get; init; } = string.Empty;
        public Guid VehiculoId { get; init; }
        public string Placa { get; init; } = string.Empty;
        public Guid PasajeroId { get; init; }
        public string PasajeroNombre { get; init; } = string.Empty;
        public Guid? EmpresaClienteId { get; init; }
        public string? EmpresaClienteNombre { get; init; }
        public string DireccionOrigen { get; init; } = string.Empty;
        public string DireccionDestino { get; init; } = string.Empty;
        public bool Urgente { get; init; }
        public bool EsPersonal { get; init; }
        public DateTimeOffset InicioProgramado { get; init; }
        public DateTimeOffset FinProgramado { get; init; }
        public DateTimeOffset? FechaHoraInicio { get; init; }
        public DateTimeOffset? FechaHoraFin { get; init; }
        public int Incidentes { get; init; }
        public int? Puntaje { get; init; }
    }
}
