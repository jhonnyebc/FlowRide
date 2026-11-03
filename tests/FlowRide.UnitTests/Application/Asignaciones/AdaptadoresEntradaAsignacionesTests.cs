using FlowRide.Application.Asignaciones.CasosUso;
using FlowRide.Application.Asignaciones.Entrada.Adaptadores;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.UnitTests.Application.Asignaciones;

public class AdaptadoresEntradaAsignacionesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class Grabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder) : ICasoUsoConRetorno<TEntrada, TSalida>
    {
        public List<TEntrada> Recibidos { get; } = [];

        public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
        {
            Recibidos.Add(datos);
            return Task.FromResult(responder(datos));
        }
    }

    private static Asignacion Asignacion() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        new FranjaHoraria(Ahora, Ahora.AddHours(1)), OrigenAsignacion.Manual, Ahora);

    private readonly Grabador<AsignarSolicitudDominio, ResultadoAsignacion> _asignar = new(d =>
        new ResultadoAsignacion(d.SolicitudId, AssignmentOutcome.NoCandidates, null, null, null, Guid.NewGuid(), "Sin candidatos"));
    private readonly Grabador<ConsultarColaDominio, IReadOnlyList<Guid>> _cola = new(_ => [Guid.Empty]);
    private readonly Grabador<AsignarManualDominio, Asignacion> _manual = new(_ => Asignacion());
    private readonly Grabador<DevolverAColaDominio, SolicitudViaje> _devolver = new(_ => null!);
    private readonly Grabador<ReasignarDominio, Asignacion> _reasignar = new(_ => Asignacion());
    private readonly Grabador<AbandonarTurnoDominio, IReadOnlyList<Guid>> _abandonar = new(_ => []);
    private readonly Grabador<ConsultarDecisionesDominio, IReadOnlyList<DecisionLectura>> _decisiones = new(_ => []);
    private readonly Grabador<BuscarAsignacionesDominio, PaginaResultado<AsignacionLectura>> _buscar =
        new(d => new PaginaResultado<AsignacionLectura>([], 0, d.Paginacion));

    private AsignacionesAdaptadorEntrada Adaptador() =>
        new(_asignar, _cola, _manual, _devolver, _reasignar, _abandonar, _decisiones, _buscar);

    [Theory]
    [InlineData(AssignmentOutcome.Assigned, "Asignada")]
    [InlineData(AssignmentOutcome.NoCandidates, "SinCandidatos")]
    [InlineData(AssignmentOutcome.InvalidConfiguration, "ConfiguracionInvalida")]
    [InlineData(null, "NoAplica")]
    public async Task Asignar_TraduceElResultado(AssignmentOutcome? resultado, string esperado)
    {
        var asignar = new Grabador<AsignarSolicitudDominio, ResultadoAsignacion>(d =>
            new ResultadoAsignacion(d.SolicitudId, resultado, null, null, null, null, "motivo"));
        var adaptador = new AsignacionesAdaptadorEntrada(asignar, _cola, _manual, _devolver, _reasignar, _abandonar, _decisiones, _buscar);

        Assert.Equal(esperado, (await adaptador.AsignarAsync(Guid.NewGuid(), default)).Resultado);
    }

    [Fact]
    public async Task Intervenciones_ExigenCandidatoMotivoYActor()
    {
        var conductorId = Guid.NewGuid();
        var vehiculoId = Guid.NewGuid();
        var peticion = new CandidatoManualPeticion { ConductorId = conductorId, VehiculoId = vehiculoId, Motivo = " Contingencia " };

        await Adaptador().AsignarManualAsync(Guid.NewGuid(), peticion, Actor, default);
        var reasignada = await Adaptador().ReasignarAsync(Guid.NewGuid(), peticion, Actor, default);
        await Adaptador().DevolverAColaAsync(Guid.NewGuid(), new MotivoPeticion { Motivo = "Reintentar" }, Actor, default);
        await Adaptador().AbandonarTurnoAsync(conductorId, Guid.NewGuid(), new MotivoPeticion { Motivo = "Enfermo" }, null, default);

        Assert.Equal(("Contingencia", conductorId, vehiculoId), (_manual.Recibidos[0].Motivo, _manual.Recibidos[0].ConductorId, _manual.Recibidos[0].VehiculoId));
        Assert.Equal(Actor, _reasignar.Recibidos[0].ActorId);
        Assert.Equal("Manual", reasignada.Origen);
        Assert.Equal("Reintentar", _devolver.Recibidos[0].Motivo);
        Assert.Null(_abandonar.Recibidos[0].ActorId);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Adaptador().AsignarManualAsync(Guid.NewGuid(), peticion with { VehiculoId = null }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Adaptador().ReasignarAsync(Guid.NewGuid(), peticion, null, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Adaptador().DevolverAColaAsync(Guid.NewGuid(), new MotivoPeticion(), Actor, default));
    }

    [Fact]
    public async Task ConsultasYCola()
    {
        Assert.Single(await Adaptador().PendientesAsync(5, default));
        Assert.Empty(await Adaptador().DecisionesAsync(Guid.NewGuid(), default));
        var pagina = await Adaptador().BuscarAsync(new BusquedaAsignacionesPeticion { Estado = "cancelada", Pagina = 2 }, default);

        Assert.Equal(EstadoAsignacion.Cancelada, _buscar.Recibidos[0].Filtro.Estado);
        Assert.Equal(2, pagina.Pagina);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Adaptador().BuscarAsync(new BusquedaAsignacionesPeticion { Estado = "Perdida" }, default));
    }

    [Fact]
    public async Task ConfiguracionDelMotor_ValidaLasReglasYMapea()
    {
        var crear = new Grabador<CrearConfiguracionDominio, ConfiguracionAsignacion>(d => ConfiguracionesEnMemoria.Nueva(2, d.Reglas));
        var activar = new Grabador<ActivarConfiguracionDominio, ConfiguracionAsignacion>(d => ConfiguracionesEnMemoria.Nueva(d.Version, activa: true));
        var consultar = new Grabador<ConsultarConfiguracionesDominio, IReadOnlyList<ConfiguracionAsignacion>>(_ => [ConfiguracionesEnMemoria.Nueva(1, activa: true)]);
        var adaptador = new ConfiguracionMotorAdaptadorEntrada(consultar, crear, activar);
        var reglas = ConfiguracionInicial.Reglas
            .Select(r => new ReglaPeticion { Codigo = r.Codigo, Habilitada = r.Habilitada, Orden = r.Orden, Peso = r.Peso, Parametros = r.Parametros })
            .ToList();

        var creada = await adaptador.CrearAsync(new ConfiguracionMotorPeticion { Reglas = reglas }, Actor, default);
        var activa = await adaptador.ActivarAsync(1, Actor, default);
        var todas = await adaptador.ConsultarAsync(default);

        Assert.Equal((2, "Borrador", 7), (creada.Version, creada.Estado, creada.Reglas.Count));
        Assert.Equal("7", creada.Reglas.Single(r => r.Codigo == "WorkloadBalanceRule").Parametros["ventanaDias"]);
        Assert.Equal("Activa", activa.Estado);
        Assert.Single(todas);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.CrearAsync(new ConfiguracionMotorPeticion(), Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.CrearAsync(
            new ConfiguracionMotorPeticion { Reglas = [new ReglaPeticion { Codigo = "AvailabilityRule" }] }, Actor, default));
    }
}
