using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Puertos;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.Application.Viajes.Entrada.Adaptadores;

public sealed class ViajesAdaptadorEntrada(
    ICasoUsoConRetorno<AceptarAsignacionDominio, Viaje> aceptar,
    ICasoUsoConRetorno<RechazarAsignacionDominio, Asignacion> rechazar,
    ICasoUsoConRetorno<ConsultarSinRespuestaDominio, IReadOnlyList<Guid>> sinRespuesta,
    ICasoUsoConRetorno<VencerAsignacionDominio, bool> vencer,
    ICasoUsoConRetorno<IniciarViajeDominio, Viaje> iniciar,
    ICasoUsoConRetorno<FinalizarViajeDominio, Viaje> finalizar,
    ICasoUsoConRetorno<RegistrarParadaDominio, Parada> registrarParada,
    ICasoUsoConRetorno<RegistrarLlegadaDominio, Parada> registrarLlegada,
    ICasoUsoConRetorno<ReportarIncidenteDominio, Incidente> reportarIncidente,
    ICasoUsoConRetorno<CalificarViajeDominio, Calificacion> calificar,
    ICasoUsoConRetorno<BuscarViajesDominio, PaginaResultado<ViajeLectura>> buscar,
    ICasoUsoConRetorno<ConsultarViajeDominio, ViajeDetalleLectura> consultar,
    ICasoUsoConRetorno<MonitorearViajesDominio, IReadOnlyList<MonitoreoViajeLectura>> monitorear,
    ICasoUsoConRetorno<TrazabilidadSolicitudDominio, TrazabilidadLectura> trazabilidad) : IViajesPuertoEntrada
{
    public async Task<ViajeRespuesta> AceptarAsignacionAsync(Guid asignacionId, Guid? actorId, CancellationToken ct) =>
        Respuesta(await aceptar.EjecutarAsync(new AceptarAsignacionDominio(asignacionId, Actor(actorId)), ct));

    public async Task RechazarAsignacionAsync(Guid asignacionId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        var motivo = string.IsNullOrWhiteSpace(peticion?.Motivo)
            ? throw new FlowRideExcepcion("motivo es obligatorio.")
            : peticion.Motivo.Trim();
        await rechazar.EjecutarAsync(new RechazarAsignacionDominio(asignacionId, motivo, Actor(actorId)), ct);
    }

    public Task<IReadOnlyList<Guid>> SinRespuestaAsync(int maximo, CancellationToken ct) =>
        sinRespuesta.EjecutarAsync(new ConsultarSinRespuestaDominio(maximo), ct);

    public Task<bool> VencerAsignacionAsync(Guid asignacionId, CancellationToken ct) =>
        vencer.EjecutarAsync(new VencerAsignacionDominio(asignacionId), ct);

    public async Task<ViajeRespuesta> IniciarAsync(Guid viajeId, Guid? actorId, CancellationToken ct) =>
        Respuesta(await iniciar.EjecutarAsync(new IniciarViajeDominio(viajeId, Actor(actorId)), ct));

    public async Task<ViajeRespuesta> FinalizarAsync(Guid viajeId, Guid? actorId, CancellationToken ct) =>
        Respuesta(await finalizar.EjecutarAsync(new FinalizarViajeDominio(viajeId, Actor(actorId)), ct));

    public async Task<ParadaRespuesta> RegistrarParadaAsync(Guid viajeId, ParadaPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Direccion) || peticion.Orden is not { } orden || peticion.HoraEstimada is not { } hora)
            throw new FlowRideExcepcion("direccion, orden y horaEstimada son obligatorios.");
        if (orden <= 0)
            throw new FlowRideExcepcion("orden debe ser mayor que cero.");

        return Respuesta(await registrarParada.EjecutarAsync(
            new RegistrarParadaDominio(viajeId, peticion.Direccion.Trim(), orden, hora, Actor(actorId)), ct));
    }

    public async Task<ParadaRespuesta> RegistrarLlegadaAsync(Guid viajeId, Guid paradaId, Guid? actorId, CancellationToken ct) =>
        Respuesta(await registrarLlegada.EjecutarAsync(new RegistrarLlegadaDominio(viajeId, paradaId, Actor(actorId)), ct));

    public async Task<IncidenteRespuesta> ReportarIncidenteAsync(Guid viajeId, IncidentePeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Descripcion))
            throw new FlowRideExcepcion("tipo y descripcion son obligatorios.");
        var tipo = Enumeracion<TipoIncidente>(peticion.Tipo, "tipo")
                   ?? throw new FlowRideExcepcion("tipo y descripcion son obligatorios.");

        var incidente = await reportarIncidente.EjecutarAsync(
            new ReportarIncidenteDominio(viajeId, tipo, peticion.Descripcion.Trim(), Actor(actorId)), ct);
        return new IncidenteRespuesta(incidente.Id, incidente.ViajeId, incidente.Tipo.ToString(), incidente.Descripcion, incidente.FechaHora,
            incidente.ReportadoPorId);
    }

    public async Task<CalificacionRespuesta> CalificarAsync(Guid viajeId, CalificacionPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (peticion?.Puntaje is not { } puntaje)
            throw new FlowRideExcepcion("puntaje es obligatorio.");
        if (puntaje is < Calificacion.PuntajeMinimo or > Calificacion.PuntajeMaximo)
            throw new FlowRideExcepcion($"puntaje debe estar entre {Calificacion.PuntajeMinimo} y {Calificacion.PuntajeMaximo}.");

        var c = await calificar.EjecutarAsync(new CalificarViajeDominio(viajeId, puntaje, peticion.Comentario, Actor(actorId)), ct);
        return new CalificacionRespuesta(c.Id, c.ViajeId, c.PasajeroId, c.Puntaje, c.Comentario, c.FechaHora);
    }

    public async Task<PaginaRespuesta<ViajeLectura>> BuscarAsync(BusquedaViajesPeticion peticion, string rol, CancellationToken ct)
    {
        var filtro = new FiltroViajes(Enumeracion<EstadoViaje>(peticion?.Estado, "estado"), peticion?.ConductorId, peticion?.PasajeroId,
            peticion?.EmpresaClienteId, peticion?.Desde, peticion?.Hasta);
        var pagina = await buscar.EjecutarAsync(new BuscarViajesDominio(filtro, new Paginacion(peticion?.Pagina, peticion?.TamanoPagina), Lector(rol)), ct);
        return new PaginaRespuesta<ViajeLectura>(pagina.Elementos, pagina.Total, pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);
    }

    public Task<ViajeDetalleLectura> ConsultarAsync(Guid viajeId, string rol, CancellationToken ct) =>
        consultar.EjecutarAsync(new ConsultarViajeDominio(viajeId, Lector(rol)), ct);

    public Task<IReadOnlyList<MonitoreoViajeLectura>> MonitorearAsync(string rol, CancellationToken ct) =>
        monitorear.EjecutarAsync(new MonitorearViajesDominio(Lector(rol)), ct);

    public Task<TrazabilidadLectura> TrazabilidadAsync(Guid solicitudId, string rol, CancellationToken ct) =>
        trazabilidad.EjecutarAsync(new TrazabilidadSolicitudDominio(solicitudId, Lector(rol)), ct);

    private static TEnum? Enumeracion<TEnum>(string? valor, string campo) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        return Enum.TryParse<TEnum>(valor.Trim(), ignoreCase: true, out var resultado) && Enum.IsDefined(resultado)
            ? resultado
            : throw new FlowRideExcepcion($"{campo} debe ser uno de: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static Guid Actor(Guid? actorId) => actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.");

    private static LectorSolicitud Lector(string rol) =>
        rol == RolesPanel.OperadorLogistico ? LectorSolicitud.OperadorLogistico : LectorSolicitud.Administrador;

    private static ViajeRespuesta Respuesta(Viaje v) => new(v.Id, v.AsignacionId, v.Estado.ToString(), v.FechaHoraInicio, v.FechaHoraFin);

    private static ParadaRespuesta Respuesta(Parada p) => new(p.Id, p.ViajeId, p.Orden, p.Direccion, p.HoraEstimada, p.HoraReal);
}
