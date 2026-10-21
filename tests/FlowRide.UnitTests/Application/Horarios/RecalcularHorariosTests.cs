using System.Reflection;
using FlowRide.Application.Horarios.CasosUso;
using FlowRide.Application.Horarios.Entrada.Adaptadores;
using FlowRide.Application.Horarios.Salida.Puertos;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Horarios;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.UnitTests.Application.Identidad;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Horarios;

internal sealed class HorariosFuturosEnMemoria : IHorariosFuturos
{
    public List<(SolicitudViaje Solicitud, IReadOnlyList<Asignacion> Vigentes)> Solicitudes { get; } = [];
    public List<Turno> Turnos { get; } = [];
    public List<Parada> Paradas { get; } = [];
    public DateTimeOffset? Desde { get; private set; }

    public Task<IReadOnlyList<(SolicitudViaje Solicitud, IReadOnlyList<Asignacion> Vigentes)>> SolicitudesAsync(DateTimeOffset desde,
        CancellationToken ct)
    {
        Desde = desde;
        return Task.FromResult<IReadOnlyList<(SolicitudViaje, IReadOnlyList<Asignacion>)>>(Solicitudes);
    }

    public Task<IReadOnlyList<Turno>> TurnosAsync(DateTimeOffset desde, CancellationToken ct) => Task.FromResult<IReadOnlyList<Turno>>(Turnos);

    public Task<IReadOnlyList<Parada>> ParadasAsync(DateTimeOffset desde, CancellationToken ct) => Task.FromResult<IReadOnlyList<Parada>>(Paradas);
}

public class RecalcularHorariosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Servicio = new(2026, 10, 25, 13, 0, 0, TimeSpan.Zero);
    private readonly HorariosFuturosEnMemoria _horarios = new();
    private readonly UnidadTrabajoEnMemoria _unidadTrabajo = new(new AuditoriaEnMemoria());

    /// <summary>Simula que el instante guardado se calculó con reglas viejas de la zona (una hora de diferencia).</summary>
    private static void Envejecer<T>(T entidad, string propiedad, object valor) =>
        typeof(T).GetProperty(propiedad, BindingFlags.Public | BindingFlags.Instance)!.SetValue(entidad, valor);

    [Fact]
    public async Task CuandoCambianLasReglas_RecalculaYLasAsignacionesVigentesSiguenAlServicio()
    {
        var solicitud = new SolicitudViaje(Guid.NewGuid(), Guid.NewGuid(), "Hotel", "Aeropuerto", Servicio.Local(), TimeSpan.FromMinutes(45), 2,
            CanalSolicitud.PanelWeb, Ahora);
        var vigente = new Asignacion(Guid.NewGuid(), solicitud.Id, Guid.NewGuid(), Guid.NewGuid(), solicitud.Franja(), OrigenAsignacion.Manual, Ahora);
        Envejecer(solicitud, nameof(SolicitudViaje.FechaHoraServicio), Servicio.AddHours(1));
        Envejecer(vigente, nameof(Asignacion.Franja), new FranjaHoraria(Servicio.AddHours(1), Servicio.AddHours(2)));
        var turno = new Turno(Guid.NewGuid(), Guid.NewGuid(), Servicio.AddHours(-2).Local(), Servicio.AddHours(6).Local());
        Envejecer(turno, nameof(Turno.Franja), new FranjaHoraria(Servicio, Servicio.AddHours(8)));
        var parada = new Parada(Guid.NewGuid(), Guid.NewGuid(), 1, "Terminal", Servicio.AddMinutes(20).Local());
        Envejecer(parada, nameof(Parada.HoraEstimada), Servicio.AddMinutes(80));
        _horarios.Solicitudes.Add((solicitud, [vigente]));
        _horarios.Turnos.Add(turno);
        _horarios.Paradas.Add(parada);
        var casoUso = new RecalcularHorariosCasoUso(_horarios, _unidadTrabajo);

        var primera = await casoUso.EjecutarAsync(new RecalcularHorariosDominio(Ahora), CancellationToken.None);
        var segunda = await casoUso.EjecutarAsync(new RecalcularHorariosDominio(Ahora), CancellationToken.None);

        Assert.Equal(new ResultadoRecalculoHorarios(1, 1, 1, 1), primera);
        Assert.Equal(0, segunda.Total);
        Assert.Equal(1, _unidadTrabajo.Confirmaciones);
        Assert.Equal((Servicio, Servicio), (solicitud.FechaHoraServicio, vigente.Franja.Inicio));
        Assert.Equal(Servicio.AddHours(-2), turno.Franja.Inicio);
        Assert.Equal(Servicio.AddMinutes(20), parada.HoraEstimada);
    }

    [Fact]
    public async Task UnaAsignacionCerrada_NoSeMueve_YElAdaptadorMiraDesdeAyer()
    {
        var solicitud = new SolicitudViaje(Guid.NewGuid(), Guid.NewGuid(), "Hotel", "Aeropuerto", Servicio.Local(), TimeSpan.FromMinutes(45), 2,
            CanalSolicitud.PanelWeb, Ahora);
        var cerrada = new Asignacion(Guid.NewGuid(), solicitud.Id, Guid.NewGuid(), Guid.NewGuid(), solicitud.Franja(), OrigenAsignacion.Manual, Ahora);
        cerrada.Rechazar("Vehículo en taller");
        var franjaVieja = new FranjaHoraria(Servicio.AddHours(1), Servicio.AddHours(2));
        Envejecer(solicitud, nameof(SolicitudViaje.FechaHoraServicio), Servicio.AddHours(1));
        Envejecer(cerrada, nameof(Asignacion.Franja), franjaVieja);
        _horarios.Solicitudes.Add((solicitud, [cerrada]));

        var cambiados = await new HorariosAdaptadorEntrada(new RecalcularHorariosCasoUso(_horarios, _unidadTrabajo), new RelojFijo(Ahora))
            .RecalcularAsync(CancellationToken.None);

        Assert.Equal(1, cambiados);
        Assert.Equal(franjaVieja, cerrada.Franja);
        Assert.Equal(Ahora - HorariosAdaptadorEntrada.Margen, _horarios.Desde);
    }

    [Fact]
    public void Turno_ExigeLaMismaZonaEnInicioYFin() =>
        Assert.Throws<ArgumentException>(() => new Turno(Guid.NewGuid(), Guid.NewGuid(), Servicio.Local(),
            FechaHoraLocal.De(Servicio.AddHours(8), "Europe/Madrid")));
}
