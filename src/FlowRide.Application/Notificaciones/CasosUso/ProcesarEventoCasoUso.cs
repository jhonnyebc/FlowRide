using System.Globalization;
using System.Text.Json;
using FlowRide.Application.Canal.Bot;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Canal.Repositorios;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.GestionNotificaciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Solicitudes.Repositorios;

namespace FlowRide.Application.Notificaciones.CasosUso;

/// <summary>
/// Convierte un evento de la Outbox en notificaciones (RF-16, RF-17, RF-35, RF-43 a RF-45; matriz decidida el 2026-10-18):
/// conductor (asignaciones nuevas, retiradas o canceladas), pasajero (conductor asignado, cambio de conductor, inicio,
/// fin, retraso, cancelación por el operador), recordatorios y alertas del panel. Todo se guarda en la transacción del
/// worker, que marca el evento procesado: un reintento no duplica mensajes (ADR-0015). Devuelve false si el tipo de
/// evento no notifica nada.
/// </summary>
public sealed class ProcesarEventoCasoUso(
    GatewayNotificaciones gateway,
    ISolicitudRepositorio solicitudes,
    IAsignacionRepositorio asignaciones,
    IConductorRepositorio conductores,
    IVehiculoRepositorio vehiculos,
    IUsuarioRepositorio usuarios,
    IMensajeSalienteRepositorio salientes,
    ParametrosDominio parametros,
    ZonaHorariaOperador zona,
    IBandejaSalida bandeja,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ProcesarEventoDominio, bool>
{
    private sealed record Servicio(Asignacion Asignacion, SolicitudViaje Solicitud);

    public async Task<bool> EjecutarAsync(ProcesarEventoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        using var documento = JsonDocument.Parse(datos.Contenido);
        var e = documento.RootElement;
        var id = datos.EventId;

        var atendido = datos.TipoEvento switch
        {
            EventosSalida.AsignacionCreada => await AsignacionNuevaAsync(Id(e, "asignacionId"), id, ct),
            EventosSalida.AsignacionReasignada => await ReasignadaAsync(Id(e.GetProperty("anterior"), "asignacionId"),
                Id(e.GetProperty("nueva"), "asignacionId"), Texto(e, "motivo"), id, ct),
            EventosSalida.AsignacionCancelada => await CanceladaAsync(Id(e, "asignacionId"), Texto(e, "motivo"), id, ct),
            EventosSalida.SolicitudCancelada => await SolicitudCanceladaAsync(e, id, ct),
            EventosSalida.AsignacionAceptada => await AceptadaAsync(Id(e, "asignacionId"), id, ct),
            EventosSalida.AsignacionRechazada => await RechazadaAsync(e, id, ct),
            EventosSalida.SolicitudSinCandidatos => await SinCandidatosAsync(e, id, ct),
            EventosSalida.TurnoAbandonado => await TurnoAbandonadoAsync(e, id, ct),
            EventosSalida.ViajeIniciado => await ViajeAsync(Id(e, "asignacionId"), "viaje.pasajero.iniciado", id, ct),
            EventosSalida.ViajeFinalizado => await ViajeAsync(Id(e, "asignacionId"), "viaje.pasajero.finalizado", id, ct),
            EventosSalida.IncidenteReportado => await IncidenteAsync(e, id, ct),
            EventosSalida.RecordatorioServicio => await RecordatorioAsync(Id(e, "asignacionId"), id, ct),
            EventosSalida.EnvioWhatsAppFallido => await EnvioFallidoAsync(e, id, ct),
            _ => false
        };

        if (atendido)
            await unidadTrabajo.GuardarCambiosAsync(ct);
        return atendido;
    }

    // ---------- Conductor ----------

    private async Task<bool> AsignacionNuevaAsync(Guid asignacionId, Guid eventId, CancellationToken ct)
    {
        if (await ServicioAsync(asignacionId, ct) is not { } servicio)
            return false;

        await gateway.NotificarAsync(servicio.Asignacion.ConductorId, "asignacion.conductor.nueva", Recorrido(servicio.Solicitud), eventId, ct);
        await ProgramarRecordatorioAsync(servicio, ct);
        return true;
    }

    private async Task<bool> ReasignadaAsync(Guid anteriorId, Guid nuevaId, string motivo, Guid eventId, CancellationToken ct)
    {
        if (await ServicioAsync(anteriorId, ct) is { } anterior)
        {
            await gateway.NotificarAsync(anterior.Asignacion.ConductorId, "asignacion.conductor.retirada",
                Con(Recorrido(anterior.Solicitud), ("motivo", motivo)), eventId, ct);
            await gateway.NotificarAsync(anterior.Solicitud.PasajeroId, "solicitud.pasajero.cambio_conductor",
                Valores(("fecha", Fecha(anterior.Solicitud.FechaHoraServicio))), eventId, ct);
        }
        await AsignacionNuevaAsync(nuevaId, eventId, ct);
        return true;
    }

    private async Task<bool> CanceladaAsync(Guid asignacionId, string motivo, Guid eventId, CancellationToken ct)
    {
        if (await ServicioAsync(asignacionId, ct) is not { } servicio)
            return false;
        await gateway.NotificarAsync(servicio.Asignacion.ConductorId, "asignacion.conductor.cancelada",
            Con(Recorrido(servicio.Solicitud), ("motivo", motivo)), eventId, ct);
        return true;
    }

    // ---------- Pasajero ----------

    private async Task<bool> SolicitudCanceladaAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        // Si la canceló el mismo pasajero (bot), ya lo sabe.
        if (Booleano(e, "porPasajero") || await solicitudes.ConsultarPorIdAsync(Id(e, "solicitudId"), ct) is not { } solicitud)
            return false;
        await gateway.NotificarAsync(solicitud.PasajeroId, "solicitud.pasajero.cancelada", Con(Recorrido(solicitud), ("motivo", Texto(e, "motivo"))),
            eventId, ct);
        return true;
    }

    private async Task<bool> AceptadaAsync(Guid asignacionId, Guid eventId, CancellationToken ct)
    {
        if (await ServicioAsync(asignacionId, ct) is not { } servicio)
            return false;
        var (conductor, placa) = await ConductorYPlacaAsync(servicio.Asignacion, ct);
        await gateway.NotificarAsync(servicio.Solicitud.PasajeroId, "solicitud.pasajero.conductor_asignado",
            Valores(("fecha", Fecha(servicio.Solicitud.FechaHoraServicio)), ("conductor", conductor), ("placa", placa)), eventId, ct);
        return true;
    }

    private async Task<bool> ViajeAsync(Guid asignacionId, string clave, Guid eventId, CancellationToken ct)
    {
        if (await ServicioAsync(asignacionId, ct) is not { } servicio)
            return false;
        var (conductor, _) = await ConductorYPlacaAsync(servicio.Asignacion, ct);
        await gateway.NotificarAsync(servicio.Solicitud.PasajeroId, clave, Valores(("conductor", conductor)), eventId, ct);
        return true;
    }

    private async Task<bool> RecordatorioAsync(Guid asignacionId, Guid eventId, CancellationToken ct)
    {
        // Solo si la asignación sigue vigente sin empezar y el servicio no ha pasado (pudo reasignarse o cancelarse).
        if (await ServicioAsync(asignacionId, ct) is not { } servicio
            || servicio.Asignacion.Estado is not (EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada)
            || servicio.Solicitud.FechaHoraServicio <= reloj.GetUtcNow())
            return false;

        var (conductor, placa) = await ConductorYPlacaAsync(servicio.Asignacion, ct);
        await gateway.NotificarAsync(servicio.Solicitud.PasajeroId, "recordatorio.pasajero",
            Con(Recorrido(servicio.Solicitud), ("conductor", conductor), ("placa", placa)), eventId, ct);
        await gateway.NotificarAsync(servicio.Asignacion.ConductorId, "recordatorio.conductor", Recorrido(servicio.Solicitud), eventId, ct);
        return true;
    }

    private async Task ProgramarRecordatorioAsync(Servicio servicio, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        if (servicio.Solicitud.FechaHoraServicio <= ahora)
            return;
        var anticipacion = TimeSpan.FromMinutes(await parametros.EnteroAsync(CatalogoParametros.AnticipacionRecordatorioMinutos, ct));
        var cuando = servicio.Solicitud.FechaHoraServicio - anticipacion;
        await bandeja.ProgramarAsync(EventosSalida.RecordatorioServicio, new { asignacionId = servicio.Asignacion.Id },
            cuando > ahora ? cuando : ahora, ct);
    }

    // ---------- Panel ----------

    private async Task<bool> RechazadaAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        if (!Booleano(e, "vencida") || await ServicioAsync(Id(e, "asignacionId"), ct) is not { } servicio)
            return false;
        var (conductor, _) = await ConductorYPlacaAsync(servicio.Asignacion, ct);
        await gateway.AlertarAsync("alerta.asignacion.vencida", SeveridadAlerta.Advertencia,
            Valores(("conductor", conductor), ("fecha", Fecha(servicio.Solicitud.FechaHoraServicio))), servicio.Solicitud.Id, eventId, ct);
        return true;
    }

    private async Task<bool> SinCandidatosAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        if (await solicitudes.ConsultarPorIdAsync(Id(e, "solicitudId"), ct) is not { } solicitud)
            return false;
        var urgente = Booleano(e, "urgente");
        await gateway.AlertarAsync(urgente ? "alerta.solicitud.urgente_sin_candidatos" : "alerta.solicitud.sin_candidatos",
            urgente ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia, Con(Recorrido(solicitud), ("motivo", Texto(e, "motivo"))),
            solicitud.Id, eventId, ct);
        return true;
    }

    private async Task<bool> TurnoAbandonadoAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        var conductorId = Id(e, "conductorId");
        var conductor = await conductores.ConsultarPorIdAsync(conductorId, ct);
        var devueltas = e.TryGetProperty("solicitudes", out var lista) && lista.ValueKind == JsonValueKind.Array ? lista.GetArrayLength() : 0;
        await gateway.AlertarAsync("alerta.turno.abandonado", SeveridadAlerta.Critica,
            Valores(("conductor", conductor?.Nombre ?? "desconocido"), ("motivo", Texto(e, "motivo")),
                ("solicitudes", devueltas.ToString(CultureInfo.InvariantCulture))), conductorId, eventId, ct);
        return true;
    }

    private async Task<bool> IncidenteAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        var tipo = Enum.TryParse<TipoIncidente>(Texto(e, "tipo"), out var valor) ? valor : TipoIncidente.Otro;
        var descripcion = Texto(e, "descripcion");
        var conductor = await conductores.ConsultarPorIdAsync(Id(e, "conductorId"), ct);
        var solicitud = e.TryGetProperty("solicitudId", out var s) && s.ValueKind == JsonValueKind.String
            ? await solicitudes.ConsultarPorIdAsync(s.GetGuid(), ct)
            : null;
        var fecha = solicitud is null ? "sin fecha" : Fecha(solicitud.FechaHoraServicio);

        await gateway.AlertarAsync("alerta.viaje.incidente",
            tipo is TipoIncidente.Accidente or TipoIncidente.FallaMecanica ? SeveridadAlerta.Critica : SeveridadAlerta.Advertencia,
            Valores(("tipo", NombreIncidente(tipo)), ("conductor", conductor?.Nombre ?? "desconocido"), ("fecha", fecha), ("descripcion", descripcion)),
            Id(e, "viajeId"), eventId, ct);
        if (tipo == TipoIncidente.Retraso && solicitud is not null)
            await gateway.NotificarAsync(solicitud.PasajeroId, "viaje.pasajero.retraso", Valores(("fecha", fecha), ("descripcion", descripcion)),
                eventId, ct);
        return true;
    }

    private async Task<bool> EnvioFallidoAsync(JsonElement e, Guid eventId, CancellationToken ct)
    {
        if (await salientes.ConsultarPorIdAsync(Id(e, "mensajeId"), ct) is not { } mensaje)
            return false;
        var usuario = await usuarios.ConsultarPorTelefonoAsync(mensaje.Destinatario, ct);
        await gateway.AlertarAsync(GatewayNotificaciones.ClaveFalloWhatsApp, SeveridadAlerta.Critica, Valores(
            ("destinatario", usuario?.Nombre ?? "un número no registrado"),
            ("clave", mensaje.ClaveMensaje ?? "respuesta del bot"),
            ("error", mensaje.UltimoError ?? "error del proveedor")), usuario?.Id, eventId, ct);
        return true;
    }

    // ---------- Datos ----------

    private async Task<Servicio?> ServicioAsync(Guid asignacionId, CancellationToken ct) =>
        await asignaciones.ConsultarPorIdAsync(asignacionId, ct) is { } asignacion
        && await solicitudes.ConsultarPorIdAsync(asignacion.SolicitudId, ct) is { } solicitud
            ? new Servicio(asignacion, solicitud)
            : null;

    private async Task<(string Conductor, string Placa)> ConductorYPlacaAsync(Asignacion asignacion, CancellationToken ct) =>
        ((await conductores.ConsultarPorIdAsync(asignacion.ConductorId, ct))?.Nombre ?? "por confirmar",
            (await vehiculos.ConsultarPorIdAsync(asignacion.VehiculoId, ct))?.Placa ?? "por confirmar");

    private Dictionary<string, string> Recorrido(SolicitudViaje s) => Valores(("fecha", Fecha(s.FechaHoraServicio)),
        ("origen", s.DireccionOrigen), ("destino", s.DireccionDestino), ("pasajeros", s.CantidadPasajeros.ToString(CultureInfo.InvariantCulture)));

    private string Fecha(DateTimeOffset instante) => TextosBot.Fecha(zona.Local(instante));

    private static Dictionary<string, string> Valores(params (string Clave, string Valor)[] pares) =>
        pares.ToDictionary(p => p.Clave, p => p.Valor, StringComparer.Ordinal);

    private static Dictionary<string, string> Con(Dictionary<string, string> valores, params (string Clave, string Valor)[] mas)
    {
        foreach (var (clave, valor) in mas) valores[clave] = valor;
        return valores;
    }

    private static string NombreIncidente(TipoIncidente tipo) => tipo switch
    {
        TipoIncidente.Retraso => "retraso",
        TipoIncidente.FallaMecanica => "falla mecánica",
        TipoIncidente.AbandonoTurno => "abandono de turno",
        TipoIncidente.Accidente => "accidente",
        _ => "otra novedad"
    };

    private static Guid Id(JsonElement e, string propiedad) => e.GetProperty(propiedad).GetGuid();

    private static string Texto(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() ?? string.Empty : string.Empty;

    private static bool Booleano(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.True;
}
