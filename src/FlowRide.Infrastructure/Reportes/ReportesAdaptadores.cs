using FlowRide.Application.Reportes.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Reportes;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FlowRide.Infrastructure.Reportes;

/// <summary>
/// Lado de lectura de los reportes: en dos pasos (las filas principales y luego sus asignaciones, viajes o vencimientos
/// por id) para que cada consulta use sus índices y no haya subconsultas correlacionadas por fila.
/// </summary>
public sealed class ConsultasReportesEf(FlowRideDbContext contexto) : IConsultasReportes
{
    private static readonly string AccionVencer = AccionesAuditoria.De(AccionesAuditoria.RecursoAsignacion, AccionesAuditoria.Vencer);

    public async Task<IReadOnlyList<SolicitudReporteLectura>> SolicitudesAsync(PeriodoReporte periodo, Guid? empresaClienteId, int maximo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        var consulta =
            from s in contexto.Solicitudes.AsNoTracking()
            join p in contexto.Pasajeros on s.PasajeroId equals p.Id
            join e in contexto.EmpresasCliente on s.EmpresaClienteId equals e.Id into empresas
            from e in empresas.DefaultIfEmpty()
            where s.FechaHoraServicio >= periodo.Desde && s.FechaHoraServicio < periodo.Hasta
            select new
            {
                s.Id,
                s.PasajeroId,
                Pasajero = p.Nombre,
                s.EmpresaClienteId,
                Empresa = e == null ? null : e.Nombre,
                s.DireccionOrigen,
                s.DireccionDestino,
                s.FechaHoraServicio,
                s.CantidadPasajeros,
                s.Urgente,
                s.EsPersonal,
                s.Canal,
                s.Estado,
                s.RegistradaEn
            };
        if (empresaClienteId is { } empresaId)
            consulta = consulta.Where(s => s.EmpresaClienteId == empresaId);
        var solicitudes = await consulta.OrderBy(s => s.FechaHoraServicio).ThenBy(s => s.Id).Take(maximo).ToListAsync(ct);

        var ids = solicitudes.Select(s => s.Id).ToList();
        var ultimas = (await Asignaciones().Where(a => ids.Contains(a.SolicitudId)).ToListAsync(ct))
            .GroupBy(a => a.SolicitudId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AsignadaEn).ThenByDescending(a => a.Id).First());

        return solicitudes.Select(s =>
        {
            var a = ultimas.GetValueOrDefault(s.Id);
            return new SolicitudReporteLectura(s.Id, s.PasajeroId, s.Pasajero, s.EmpresaClienteId, s.Empresa, s.DireccionOrigen,
                s.DireccionDestino, s.FechaHoraServicio, s.CantidadPasajeros, s.Urgente, s.EsPersonal, s.Canal, s.Estado, s.RegistradaEn, a?.Id,
                a?.Estado, a?.ConductorId, a?.Conductor, a?.Placa, a?.ViajeId, a?.EstadoViaje, a?.InicioReal, a?.FinReal, a?.Incidentes ?? 0,
                a?.Puntaje);
        }).ToList();
    }

    public async Task<IReadOnlyList<AsignacionReporteLectura>> AsignacionesAsync(PeriodoReporte periodo, Guid? conductorId, int maximo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        var consulta = Asignaciones().Where(a => a.InicioProgramado >= periodo.Desde && a.InicioProgramado < periodo.Hasta);
        if (conductorId is { } id)
            consulta = consulta.Where(a => a.ConductorId == id);
        var asignaciones = await consulta.OrderBy(a => a.InicioProgramado).ThenBy(a => a.Id).Take(maximo).ToListAsync(ct);

        var rechazadas = asignaciones.Where(a => a.Estado == EstadoAsignacion.Rechazada).Select(a => a.Id.ToString()).ToList();
        var vencidas = (await contexto.EventosAuditoria.AsNoTracking()
                .Where(e => e.Accion == AccionVencer && rechazadas.Contains(e.RecursoId))
                .Select(e => e.RecursoId).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        return asignaciones.Select(a => new AsignacionReporteLectura(a.Id, a.SolicitudId, a.ConductorId, a.Conductor, a.Placa, a.Origen, a.Estado,
                vencidas.Contains(a.Id.ToString()), a.InicioProgramado, a.ViajeId, a.EstadoViaje, a.InicioReal, a.Incidentes, a.Puntaje))
            .ToList();
    }

    // Asignación con su conductor, vehículo, viaje (si lo aceptó) y calificación.
    private IQueryable<FilaAsignacion> Asignaciones() =>
        from a in contexto.Asignaciones.AsNoTracking()
        join c in contexto.Conductores on a.ConductorId equals c.Id
        join ve in contexto.Vehiculos on a.VehiculoId equals ve.Id
        join v in contexto.Viajes on a.Id equals v.AsignacionId into viajes
        from v in viajes.DefaultIfEmpty()
        select new FilaAsignacion
        {
            Id = a.Id,
            SolicitudId = a.SolicitudId,
            ConductorId = a.ConductorId,
            Conductor = c.Nombre,
            Placa = ve.Placa,
            Origen = a.Origen,
            Estado = a.Estado,
            AsignadaEn = a.AsignadaEn,
            InicioProgramado = a.Franja.Inicio,
            ViajeId = v == null ? null : v.Id,
            EstadoViaje = v == null ? null : v.Estado,
            InicioReal = v == null ? null : v.FechaHoraInicio,
            FinReal = v == null ? null : v.FechaHoraFin,
            Incidentes = v == null ? 0 : v.Incidentes.Count,
            Puntaje = v == null ? null : contexto.Calificaciones.Where(cal => cal.ViajeId == v.Id).Select(cal => (int?)cal.Puntaje).FirstOrDefault()
        };

    private sealed class FilaAsignacion
    {
        public Guid Id { get; init; }
        public Guid SolicitudId { get; init; }
        public Guid ConductorId { get; init; }
        public string Conductor { get; init; } = string.Empty;
        public string Placa { get; init; } = string.Empty;
        public OrigenAsignacion Origen { get; init; }
        public EstadoAsignacion Estado { get; init; }
        public DateTimeOffset AsignadaEn { get; init; }
        public DateTimeOffset InicioProgramado { get; init; }
        public Guid? ViajeId { get; init; }
        public EstadoViaje? EstadoViaje { get; init; }
        public DateTimeOffset? InicioReal { get; init; }
        public DateTimeOffset? FinReal { get; init; }
        public int Incidentes { get; init; }
        public int? Puntaje { get; init; }
    }
}

/// <summary>Opciones de la caché de indicadores de periodos cerrados (ADR-0006).</summary>
public sealed class CacheReportesOptions
{
    public const string Seccion = "FlowRide:Cache";
    public static readonly TimeSpan TtlMaximo = TimeSpan.FromMinutes(10);

    public int TtlIndicadoresSegundos { get; set; } = 300;

    public TimeSpan Ttl => TimeSpan.FromSeconds(Math.Clamp(TtlIndicadoresSegundos, 1, (int)TtlMaximo.TotalSeconds));
}

/// <summary>
/// Cache-aside en memoria de la instancia (ADR-0006) con TTL ≤ 10 min: si se pierde o vence, se vuelve a
/// calcular contra PostgreSQL. Solo la usan los indicadores de periodos cerrados.
/// </summary>
public sealed class CacheReportesMemoria(IMemoryCache cache, CacheReportesOptions opciones) : ICacheReportes
{
    public async Task<T> ObtenerOCrearAsync<T>(string clave, Func<CancellationToken, Task<T>> crear, CancellationToken ct) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentNullException.ThrowIfNull(crear);
        if (cache.TryGetValue(clave, out T? enCache) && enCache is not null)
            return enCache;

        var valor = await crear(ct);
        cache.Set(clave, valor, opciones.Ttl);
        return valor;
    }
}
