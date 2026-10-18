using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes;
using FlowRide.Domain.Viajes.Repositorios;

namespace FlowRide.Application.Solicitudes.CasosUso;

/// <summary>
/// Recibe una solicitud, la valida (RF-29) y la deja Pendiente en la cola de asignación (RF-09). La usa el Panel Web
/// (registrando por el pasajero) y, en la Fase 7, el bot de WhatsApp.
/// </summary>
public sealed class RegistrarSolicitudCasoUso(
    PasajeroDominio pasajeroDominio,
    EmpresaClienteDominio empresaDominio,
    SolicitudDominio solicitudDominio,
    IItinerarioRepositorio itinerarios,
    ISolicitudRepositorio solicitudes,
    ZonaHorariaOperador zona,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarSolicitudDominio, SolicitudViaje>
{
    public async Task<SolicitudViaje> EjecutarAsync(RegistrarSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();

        var pasajero = await pasajeroDominio.ValidarExistaAsync(datos.PasajeroId, ct);
        var empresa = await empresaDominio.ValidarExistaAsync(pasajero.EmpresaClienteId, ct);
        SolicitudDominio.ValidarPuedeSolicitar(pasajero, empresa);

        // Servicio programado: origen, destino y horario salen del itinerario de la empresa.
        string? origen = datos.DireccionOrigen, destino = datos.DireccionDestino;
        DateTimeOffset? fechaHora = datos.FechaHoraServicio;
        TimeSpan? duracion = datos.DuracionEstimada;
        if (datos.ItinerarioId is { } itinerarioId)
        {
            var itinerario = await itinerarios.ConsultarPorIdAsync(itinerarioId, ct)
                             ?? throw new FlowRideExcepcion($"El itinerario {itinerarioId} no existe.");
            ItinerarioDominio.ValidarDeLaEmpresa(itinerario, empresa.Id);
            origen = itinerario.Origen;
            destino = itinerario.Destino;
            fechaHora = zona.Instante(itinerario.Fecha, itinerario.HoraSalida);
            duracion = zona.Instante(itinerario.Fecha, itinerario.HoraLlegada) - fechaHora;
        }

        if (string.IsNullOrWhiteSpace(origen) || string.IsNullOrWhiteSpace(destino) || fechaHora is null || duracion is null)
            throw new FlowRideExcepcion("Sin itinerario, direccionOrigen, direccionDestino, fechaHoraServicio y duracionMinutos son obligatorios.");
        SolicitudDominio.ValidarDatosMinimos(fechaHora.Value, duracion.Value, datos.CantidadPasajeros, ahora);
        await solicitudDominio.ValidarSinSolapamientoAsync(pasajero.Id,
            new FranjaHoraria(fechaHora.Value, fechaHora.Value + duracion.Value), null, ct);

        var solicitud = new SolicitudViaje(Guid.CreateVersion7(ahora), pasajero.Id, origen, destino, fechaHora.Value, duracion.Value,
            datos.CantidadPasajeros, datos.Canal, ahora, datos.Urgente, datos.EsPersonal, empresa.Id, datos.ItinerarioId, datos.ActorId);
        await solicitudes.CrearAsync(solicitud, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.Crear,
            solicitud.Id, ahora, despues: Auditar.Valores(("estado", solicitud.Estado), ("canal", solicitud.Canal),
                ("urgente", solicitud.Urgente), ("personal", solicitud.EsPersonal))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return solicitud;
    }
}

/// <summary>
/// Ajusta una solicitud sin asignar (RF-32), dentro del plazo de la ventana de cancelación. Si estaba sin candidatos,
/// vuelve a la cola para que el motor la intente de nuevo.
/// </summary>
public sealed class ModificarSolicitudCasoUso(
    SolicitudDominio solicitudDominio,
    ISolicitudRepositorio solicitudes,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ModificarSolicitudDominio, SolicitudViaje>
{
    public async Task<SolicitudViaje> EjecutarAsync(ModificarSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var solicitud = await solicitudDominio.ValidarExistaAsync(datos.SolicitudId, ct);
        SolicitudDominio.ValidarModificable(solicitud);
        await solicitudDominio.ValidarDentroDeVentanaAsync(solicitud, ahora, puedeSaltarVentana: false, ct);
        SolicitudDominio.ValidarDatosMinimos(datos.FechaHoraServicio, datos.DuracionEstimada, datos.CantidadPasajeros, ahora);
        await solicitudDominio.ValidarSinSolapamientoAsync(solicitud.PasajeroId,
            new FranjaHoraria(datos.FechaHoraServicio, datos.FechaHoraServicio + datos.DuracionEstimada), solicitud.Id, ct);

        var antes = Auditar.Valores(("estado", solicitud.Estado), ("urgente", solicitud.Urgente));
        solicitud.Modificar(datos.DireccionOrigen, datos.DireccionDestino, datos.FechaHoraServicio, datos.DuracionEstimada,
            datos.CantidadPasajeros, datos.Urgente, datos.ActorId, ahora);
        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.Modificar,
            solicitud.Id, ahora, antes, Auditar.Valores(("estado", solicitud.Estado), ("urgente", solicitud.Urgente))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return solicitud;
    }
}

/// <summary>
/// Cancela una solicitud antes del viaje, con motivo (RF-32, PAS-AUD-02). Si estaba asignada, también cancela su
/// asignación vigente para liberar al conductor y al vehículo.
/// </summary>
public sealed class CancelarSolicitudCasoUso(
    SolicitudDominio solicitudDominio,
    ISolicitudRepositorio solicitudes,
    IAsignacionRepositorio asignaciones,
    IViajeRepositorio viajes,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CancelarSolicitudDominio, SolicitudViaje>
{
    public async Task<SolicitudViaje> EjecutarAsync(CancelarSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (string.IsNullOrWhiteSpace(datos.Motivo))
            throw new FlowRideExcepcion("El motivo de la cancelación es obligatorio.");

        var ahora = reloj.GetUtcNow();
        var solicitud = await solicitudDominio.ValidarExistaAsync(datos.SolicitudId, ct);
        if (solicitud.Estado is EstadoSolicitud.EnCurso or EstadoSolicitud.Completada or EstadoSolicitud.Cancelada)
            throw new FlowRideExcepcion($"La solicitud está {solicitud.Estado} y ya no se puede cancelar.");
        await solicitudDominio.ValidarDentroDeVentanaAsync(solicitud, ahora, datos.PuedeSaltarVentana, ct);

        var estadoAnterior = solicitud.Estado;
        solicitud.Cancelar(Guid.CreateVersion7(ahora), datos.Motivo, datos.ActorId, ahora);
        if (estadoAnterior == EstadoSolicitud.Asignada
            && await asignaciones.ConsultarVigentePorSolicitudAsync(solicitud.Id, ct) is { } asignacion)
        {
            asignacion.Cancelar($"Solicitud cancelada: {datos.Motivo.Trim()}");
            await viajes.CancelarProgramadoDeAsync(asignacion.Id, ct);
        }

        await solicitudes.ModificarAsync(solicitud.Id, solicitud, ct);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoSolicitud, AccionesAuditoria.Cancelar), AccionesAuditoria.RecursoSolicitud,
            solicitud.Id.ToString(), ahora, ResultadoAuditoria.Exitoso, datos.Motivo,
            Auditar.Valores(("estado", estadoAnterior)), Auditar.Valores(("estado", solicitud.Estado))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return solicitud;
    }
}

/// <summary>Estado actual e historial de una solicitud (RF-30), respetando la visibilidad de las personales.</summary>
public sealed class ConsultarSolicitudCasoUso(IConsultasSolicitudes consultas, ParametrosDominio parametros)
    : ICasoUsoConRetorno<ConsultarSolicitudDominio, SolicitudDetalleLectura>
{
    public async Task<SolicitudDetalleLectura> EjecutarAsync(ConsultarSolicitudDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var detalle = await consultas.DetalleAsync(datos.SolicitudId, ct)
                      ?? throw new FlowRideExcepcion($"La solicitud {datos.SolicitudId} no existe.");
        var visibilidad = await parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);
        return VisibilidadSolicitudes.PuedeVerDetalle(detalle.Solicitud.EsPersonal, datos.Lector, visibilidad)
            ? detalle
            : detalle with { Solicitud = detalle.Solicitud.SinDirecciones() };
    }
}

/// <summary>Cola e historial de solicitudes por pasajero, empresa, estado o fechas (RF-31).</summary>
public sealed class BuscarSolicitudesCasoUso(IConsultasSolicitudes consultas, ParametrosDominio parametros)
    : ICasoUsoConRetorno<BuscarSolicitudesDominio, PaginaResultado<SolicitudLectura>>
{
    public async Task<PaginaResultado<SolicitudLectura>> EjecutarAsync(BuscarSolicitudesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var pagina = await consultas.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
        var visibilidad = await parametros.OpcionAsync(CatalogoParametros.VisibilidadSolicitudesPersonales, ct);
        return pagina.Convertir(solicitud =>
            VisibilidadSolicitudes.PuedeVerDetalle(solicitud.EsPersonal, datos.Lector, visibilidad) ? solicitud : solicitud.SinDirecciones());
    }
}

/// <summary>
/// Registra un servicio programado de una empresa cliente (RF-08) para un rango de fechas y días de la semana: un
/// itinerario por fecha. Si alguna fecha ya tiene ese código de servicio, no registra ninguno.
/// </summary>
public sealed class RegistrarItinerariosCasoUso(
    EmpresaClienteDominio empresaDominio,
    IItinerarioRepositorio itinerarios,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarItinerariosDominio, IReadOnlyList<Itinerario>>
{
    public async Task<IReadOnlyList<Itinerario>> EjecutarAsync(RegistrarItinerariosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ItinerarioDominio.ValidarHorario(datos.HoraSalida, datos.HoraLlegada);
        var fechas = ItinerarioDominio.Fechas(datos.FechaDesde, datos.FechaHasta, datos.Dias);
        var empresa = await empresaDominio.ValidarActivaAsync(datos.EmpresaClienteId, ct);

        var ocupadas = await itinerarios.ConsultarFechasOcupadasAsync(empresa.Id, datos.CodigoServicio.Trim(), fechas[0], fechas[^1], ct);
        var repetidas = fechas.Where(ocupadas.Contains).ToList();
        if (repetidas.Count > 0)
            throw new FlowRideExcepcion(
                $"El servicio {datos.CodigoServicio.Trim()} ya está registrado para {repetidas.Count} de esas fechas (la primera, {repetidas[0]:yyyy-MM-dd}).");

        var ahora = reloj.GetUtcNow();
        var creados = new List<Itinerario>();
        foreach (var fecha in fechas)
        {
            var itinerario = new Itinerario(Guid.CreateVersion7(ahora), empresa.Id, datos.CodigoServicio, fecha, datos.Origen,
                datos.Destino, datos.HoraSalida, datos.HoraLlegada);
            await itinerarios.CrearAsync(itinerario, ct);
            await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoItinerario, AccionesAuditoria.Crear,
                itinerario.Id, ahora, despues: Auditar.Valores(("codigo_servicio", itinerario.CodigoServicio), ("fecha", fecha))), ct);
            creados.Add(itinerario);
        }

        await unidadTrabajo.GuardarCambiosAsync(ct);
        return creados;
    }
}

public sealed class BuscarItinerariosCasoUso(IItinerarioRepositorio itinerarios)
    : ICasoUsoConRetorno<BuscarItinerariosDominio, PaginaResultado<Itinerario>>
{
    public Task<PaginaResultado<Itinerario>> EjecutarAsync(BuscarItinerariosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return itinerarios.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}
