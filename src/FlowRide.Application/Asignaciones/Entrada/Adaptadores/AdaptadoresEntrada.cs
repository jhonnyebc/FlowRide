using FlowRide.Application.Asignaciones.CasosUso;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Entrada.Puertos;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Assignment.Results;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Asignaciones.Entrada.Adaptadores;

public sealed class AsignacionesAdaptadorEntrada(
    ICasoUsoConRetorno<AsignarSolicitudDominio, ResultadoAsignacion> asignar,
    ICasoUsoConRetorno<ConsultarColaDominio, IReadOnlyList<Guid>> cola,
    ICasoUsoConRetorno<AsignarManualDominio, Asignacion> asignarManual,
    ICasoUsoConRetorno<DevolverAColaDominio, SolicitudViaje> devolver,
    ICasoUsoConRetorno<ReasignarDominio, Asignacion> reasignar,
    ICasoUsoConRetorno<AbandonarTurnoDominio, IReadOnlyList<Guid>> abandonar,
    ICasoUsoConRetorno<ConsultarDecisionesDominio, IReadOnlyList<DecisionLectura>> decisiones,
    ICasoUsoConRetorno<BuscarAsignacionesDominio, PaginaResultado<AsignacionLectura>> buscar) : IAsignacionesPuertoEntrada
{
    public async Task<ResultadoAsignacionRespuesta> AsignarAsync(Guid solicitudId, CancellationToken ct)
    {
        var r = await asignar.EjecutarAsync(new AsignarSolicitudDominio(solicitudId), ct);
        var resultado = r.Resultado switch
        {
            AssignmentOutcome.Assigned => "Asignada",
            AssignmentOutcome.NoCandidates => "SinCandidatos",
            AssignmentOutcome.InvalidConfiguration => "ConfiguracionInvalida",
            _ => "NoAplica"
        };
        return new ResultadoAsignacionRespuesta(r.SolicitudId, resultado, r.AsignacionId, r.ConductorId, r.VehiculoId, r.DecisionId, r.Motivo);
    }

    public Task<IReadOnlyList<Guid>> PendientesAsync(int maximo, CancellationToken ct) =>
        cola.EjecutarAsync(new ConsultarColaDominio(maximo), ct);

    public async Task<AsignacionRespuesta> AsignarManualAsync(Guid solicitudId, CandidatoManualPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        var (conductorId, vehiculoId, motivo) = Candidato(peticion);
        return Respuesta(await asignarManual.EjecutarAsync(new AsignarManualDominio(solicitudId, conductorId, vehiculoId, motivo, Actor(actorId)), ct));
    }

    public async Task DevolverAColaAsync(Guid solicitudId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct) =>
        await devolver.EjecutarAsync(new DevolverAColaDominio(solicitudId, Motivo(peticion?.Motivo), Actor(actorId)), ct);

    public async Task<AsignacionRespuesta> ReasignarAsync(Guid asignacionId, CandidatoManualPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        var (conductorId, vehiculoId, motivo) = Candidato(peticion);
        return Respuesta(await reasignar.EjecutarAsync(new ReasignarDominio(asignacionId, conductorId, vehiculoId, motivo, Actor(actorId)), ct));
    }

    public Task<IReadOnlyList<Guid>> AbandonarTurnoAsync(Guid conductorId, Guid turnoId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct) =>
        abandonar.EjecutarAsync(new AbandonarTurnoDominio(conductorId, turnoId, Motivo(peticion?.Motivo), actorId), ct);

    public Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct) =>
        decisiones.EjecutarAsync(new ConsultarDecisionesDominio(solicitudId), ct);

    public async Task<PaginaRespuesta<AsignacionLectura>> BuscarAsync(BusquedaAsignacionesPeticion peticion, CancellationToken ct)
    {
        EstadoAsignacion? estado = null;
        if (!string.IsNullOrWhiteSpace(peticion?.Estado))
        {
            estado = Enum.TryParse<EstadoAsignacion>(peticion.Estado.Trim(), ignoreCase: true, out var valor) && Enum.IsDefined(valor)
                ? valor
                : throw new FlowRideExcepcion($"estado debe ser uno de: {string.Join(", ", Enum.GetNames<EstadoAsignacion>())}.");
        }

        var pagina = await buscar.EjecutarAsync(new BuscarAsignacionesDominio(
            new FiltroAsignaciones(peticion?.ConductorId, peticion?.SolicitudId, estado, peticion?.Desde, peticion?.Hasta),
            new Paginacion(peticion?.Pagina, peticion?.TamanoPagina)), ct);
        return new PaginaRespuesta<AsignacionLectura>(pagina.Elementos, pagina.Total, pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);
    }

    private static (Guid ConductorId, Guid VehiculoId, string Motivo) Candidato(CandidatoManualPeticion? peticion) =>
        peticion?.ConductorId is { } conductorId && peticion.VehiculoId is { } vehiculoId && !string.IsNullOrWhiteSpace(peticion.Motivo)
            ? (conductorId, vehiculoId, peticion.Motivo.Trim())
            : throw new FlowRideExcepcion("conductorId, vehiculoId y motivo son obligatorios.");

    private static string Motivo(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? throw new FlowRideExcepcion("motivo es obligatorio.") : motivo.Trim();

    private static Guid Actor(Guid? actorId) => actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.");

    internal static AsignacionRespuesta Respuesta(Asignacion a) => new(a.Id, a.SolicitudId, a.ConductorId, a.VehiculoId, a.Franja.Inicio,
        a.Franja.Fin, a.Estado.ToString(), a.Origen.ToString(), a.AsignadaEn, a.DecisionId, a.AsignacionReemplazadaId);
}

public sealed class ConfiguracionMotorAdaptadorEntrada(
    ICasoUsoConRetorno<ConsultarConfiguracionesDominio, IReadOnlyList<ConfiguracionAsignacion>> consultar,
    ICasoUsoConRetorno<CrearConfiguracionDominio, ConfiguracionAsignacion> crear,
    ICasoUsoConRetorno<ActivarConfiguracionDominio, ConfiguracionAsignacion> activar) : IConfiguracionMotorPuertoEntrada
{
    public async Task<IReadOnlyList<ConfiguracionMotorRespuesta>> ConsultarAsync(CancellationToken ct) =>
        (await consultar.EjecutarAsync(ConsultarConfiguracionesDominio.Todas, ct)).Select(Respuesta).ToList();

    public async Task<ConfiguracionMotorRespuesta> CrearAsync(ConfiguracionMotorPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (peticion?.Reglas is not { Count: > 0 } reglas)
            throw new FlowRideExcepcion("reglas es obligatorio y no puede estar vacío.");

        var pedidas = reglas.Select(regla => regla.Codigo is { Length: > 0 } codigo && regla.Habilitada is { } habilitada
                                             && regla.Orden is { } orden && regla.Peso is { } peso
            ? new ReglaPedida(codigo.Trim(), habilitada, orden, peso, regla.Parametros)
            : throw new FlowRideExcepcion("Cada regla requiere codigo, habilitada, orden y peso.")).ToList();
        return Respuesta(await crear.EjecutarAsync(new CrearConfiguracionDominio(pedidas, actorId), ct));
    }

    public async Task<ConfiguracionMotorRespuesta> ActivarAsync(int version, Guid? actorId, CancellationToken ct) =>
        Respuesta(await activar.EjecutarAsync(new ActivarConfiguracionDominio(version, actorId), ct));

    private static ConfiguracionMotorRespuesta Respuesta(ConfiguracionAsignacion c) => new(c.Id, c.Version, c.Estado.ToString(), c.CreadaEn,
        c.ActivadaEn, c.Reglas.OrderBy(r => r.Tipo).ThenBy(r => r.Orden)
            .Select(r => new ReglaRespuesta(r.CodigoRegla, r.Tipo.ToString(), r.Habilitada, r.Orden, r.Peso, r.LeerParametros())).ToList());
}
