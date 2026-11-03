using FlowRide.Application.Comun;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Puertos;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Solicitudes.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Solicitudes.Entrada.Adaptadores;

/// <summary>
/// Solicitudes desde el Panel Web (canal PanelWeb): se registran por el pasajero. Tras cada cambio devuelve el detalle tal
/// como lo ve quien la cambió.
/// </summary>
public sealed class SolicitudesAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarSolicitudDominio, SolicitudViaje> registrar,
    ICasoUsoConRetorno<ModificarSolicitudDominio, SolicitudViaje> modificar,
    ICasoUsoConRetorno<CancelarSolicitudDominio, SolicitudViaje> cancelar,
    ICasoUsoConRetorno<ConsultarSolicitudDominio, SolicitudDetalleLectura> consultar,
    ICasoUsoConRetorno<BuscarSolicitudesDominio, PaginaResultado<SolicitudLectura>> buscar,
    ZonaHorariaOperador zona) : ISolicitudesPuertoEntrada
{
    public async Task<SolicitudDetalleRespuesta> RegistrarAsync(SolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct)
    {
        if (peticion?.PasajeroId is not { } pasajeroId || peticion.CantidadPasajeros is not { } cantidad)
            throw new FlowRideExcepcion("pasajeroId y cantidadPasajeros son obligatorios.");

        var solicitud = await registrar.EjecutarAsync(new RegistrarSolicitudDominio(
            pasajeroId, peticion.ItinerarioId, Texto(peticion.DireccionOrigen), Texto(peticion.DireccionDestino),
            HorasLocales.Opcional(peticion.FechaHoraServicio, peticion.ZonaHoraria, zona), Minutos(peticion.DuracionMinutos), cantidad, peticion.Urgente ?? false,
            peticion.EsPersonal ?? false, CanalSolicitud.PanelWeb, Actor(actorId)), ct);
        return await ConsultarAsync(solicitud.Id, rol, ct);
    }

    public async Task<SolicitudDetalleRespuesta> ModificarAsync(
        Guid solicitudId, SolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct)
    {
        if (peticion is null || string.IsNullOrWhiteSpace(peticion.DireccionOrigen) || string.IsNullOrWhiteSpace(peticion.DireccionDestino)
            || peticion.FechaHoraServicio is null || peticion.DuracionMinutos is null || peticion.CantidadPasajeros is null)
            throw new FlowRideExcepcion(
                "direccionOrigen, direccionDestino, fechaHoraServicio, duracionMinutos y cantidadPasajeros son obligatorios.");

        await modificar.EjecutarAsync(new ModificarSolicitudDominio(solicitudId, peticion.DireccionOrigen.Trim(),
            peticion.DireccionDestino.Trim(), HorasLocales.Opcional(peticion.FechaHoraServicio, peticion.ZonaHoraria, zona)!, Minutos(peticion.DuracionMinutos)!.Value,
            peticion.CantidadPasajeros.Value, peticion.Urgente ?? false, Actor(actorId)), ct);
        return await ConsultarAsync(solicitudId, rol, ct);
    }

    public async Task<SolicitudDetalleRespuesta> CancelarAsync(
        Guid solicitudId, CancelarSolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Motivo))
            throw new FlowRideExcepcion("motivo es obligatorio.");

        // Decisión del 2026-10-17: solo el Operador logístico cancela fuera de la ventana (contingencias).
        await cancelar.EjecutarAsync(new CancelarSolicitudDominio(solicitudId, peticion.Motivo.Trim(), Actor(actorId),
            PuedeSaltarVentana: rol == RolesPanel.OperadorLogistico), ct);
        return await ConsultarAsync(solicitudId, rol, ct);
    }

    public async Task<SolicitudDetalleRespuesta> ConsultarAsync(Guid solicitudId, string rol, CancellationToken ct)
    {
        var detalle = await consultar.EjecutarAsync(new ConsultarSolicitudDominio(solicitudId, Lector(rol)), ct);
        return new SolicitudDetalleRespuesta(MapeoSolicitudes.Respuesta(detalle.Solicitud),
            detalle.Historial.Select(c => new CambioEstadoRespuesta(c.EstadoAnterior?.ToString(), c.EstadoNuevo.ToString(),
                c.FechaHora, c.ActorId, c.Motivo)).ToList());
    }

    public async Task<PaginaRespuesta<SolicitudRespuesta>> BuscarAsync(BusquedaSolicitudesPeticion peticion, string rol, CancellationToken ct)
    {
        EstadoSolicitud? estado = null;
        if (!string.IsNullOrWhiteSpace(peticion?.Estado))
        {
            estado = Enum.TryParse<EstadoSolicitud>(peticion.Estado.Trim(), ignoreCase: true, out var valor) && Enum.IsDefined(valor)
                ? valor
                : throw new FlowRideExcepcion($"estado debe ser uno de: {string.Join(", ", Enum.GetNames<EstadoSolicitud>())}.");
        }

        var filtro = new FiltroSolicitudes(estado, peticion?.PasajeroId, peticion?.EmpresaClienteId, peticion?.Desde, peticion?.Hasta,
            peticion?.Urgente);
        var pagina = await buscar.EjecutarAsync(
            new BuscarSolicitudesDominio(filtro, new Paginacion(peticion?.Pagina, peticion?.TamanoPagina), Lector(rol)), ct);
        return new PaginaRespuesta<SolicitudRespuesta>(pagina.Elementos.Select(MapeoSolicitudes.Respuesta).ToList(), pagina.Total,
            pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);
    }

    private static Guid Actor(Guid? actorId) =>
        actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.");

    private static LectorSolicitud Lector(string rol) =>
        rol == RolesPanel.OperadorLogistico ? LectorSolicitud.OperadorLogistico : LectorSolicitud.Administrador;

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static TimeSpan? Minutos(int? minutos) => minutos is { } valor ? TimeSpan.FromMinutes(valor) : null;
}

public sealed class ItinerariosAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarItinerariosDominio, IReadOnlyList<Itinerario>> registrar,
    ICasoUsoConRetorno<BuscarItinerariosDominio, PaginaResultado<Itinerario>> buscar,
    ZonaHorariaOperador zona) : IItinerariosPuertoEntrada
{
    private static readonly Dictionary<string, DayOfWeek> Dias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lunes"] = DayOfWeek.Monday,
        ["martes"] = DayOfWeek.Tuesday,
        ["miércoles"] = DayOfWeek.Wednesday,
        ["miercoles"] = DayOfWeek.Wednesday,
        ["jueves"] = DayOfWeek.Thursday,
        ["viernes"] = DayOfWeek.Friday,
        ["sábado"] = DayOfWeek.Saturday,
        ["sabado"] = DayOfWeek.Saturday,
        ["domingo"] = DayOfWeek.Sunday
    };

    public async Task<IReadOnlyList<ItinerarioRespuesta>> RegistrarAsync(ItinerariosPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (peticion?.EmpresaClienteId is not { } empresaId || string.IsNullOrWhiteSpace(peticion.CodigoServicio)
            || string.IsNullOrWhiteSpace(peticion.Origen) || string.IsNullOrWhiteSpace(peticion.Destino)
            || peticion.HoraSalida is not { } salida || peticion.HoraLlegada is not { } llegada || peticion.FechaDesde is not { } desde)
            throw new FlowRideExcepcion(
                "empresaClienteId, codigoServicio, origen, destino, horaSalida, horaLlegada y fechaDesde son obligatorios.");

        var dias = (peticion.Dias ?? []).Select(dia => Dias.TryGetValue(dia.Trim(), out var valor)
            ? valor
            : throw new FlowRideExcepcion($"El día '{dia}' no es válido: usa lunes, martes, … domingo.")).ToHashSet();

        var creados = await registrar.EjecutarAsync(new RegistrarItinerariosDominio(empresaId, peticion.CodigoServicio.Trim(),
            peticion.Origen.Trim(), peticion.Destino.Trim(), salida, llegada,
            string.IsNullOrWhiteSpace(peticion.ZonaHoraria) ? zona.Zona.Id : peticion.ZonaHoraria.Trim(), desde, peticion.FechaHasta ?? desde, dias, actorId), ct);
        return creados.Select(MapeoSolicitudes.Itinerario).ToList();
    }

    public async Task<PaginaRespuesta<ItinerarioRespuesta>> BuscarAsync(BusquedaItinerariosPeticion peticion, CancellationToken ct)
    {
        var filtro = new FiltroItinerarios(peticion?.EmpresaClienteId,
            string.IsNullOrWhiteSpace(peticion?.CodigoServicio) ? null : peticion.CodigoServicio.Trim(), peticion?.Desde, peticion?.Hasta);
        var pagina = await buscar.EjecutarAsync(
            new BuscarItinerariosDominio(filtro, new Paginacion(peticion?.Pagina, peticion?.TamanoPagina)), ct);
        return new PaginaRespuesta<ItinerarioRespuesta>(pagina.Elementos.Select(MapeoSolicitudes.Itinerario).ToList(), pagina.Total,
            pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);
    }
}

internal static class MapeoSolicitudes
{
    public static SolicitudRespuesta Respuesta(SolicitudLectura s) => new(s.Id, s.PasajeroId, s.PasajeroNombre, s.EmpresaClienteId,
        s.EmpresaClienteNombre, s.ItinerarioId, s.DireccionOrigen, s.DireccionDestino, s.FechaHoraServicio,
        s.HoraLocalServicio, s.ZonaHoraria,
        (int)Math.Round(s.DuracionEstimada.TotalMinutes, MidpointRounding.AwayFromZero), s.CantidadPasajeros, s.Urgente, s.EsPersonal,
        s.Canal.ToString(), s.Estado.ToString(), s.RegistradaEn, s.DetalleRestringido);

    public static ItinerarioRespuesta Itinerario(Itinerario i) =>
        new(i.Id, i.EmpresaClienteId, i.CodigoServicio, i.Fecha, i.Origen, i.Destino, i.HoraSalida, i.HoraLlegada, i.ZonaHoraria);
}
