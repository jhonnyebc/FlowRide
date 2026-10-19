using System.Globalization;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Solicitudes.Salida.Puertos;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Solicitudes.GestionSolicitudes;
using FlowRide.Domain.Viajes.GestionViajes;

namespace FlowRide.Application.Canal.Bot;

/// <summary>
/// Lo que el bot decide ante un mensaje: la respuesta y, si el paso termina un flujo, la acción de negocio que lo cumple.
/// La acción usa los mismos casos de uso del panel y devuelve el texto de éxito; si falla por una regla de negocio, el
/// procesador descarta los cambios y responde con el motivo.
/// </summary>
public sealed record DecisionBot(string Respuesta, Func<CancellationToken, Task<string>>? Accion = null);

/// <summary>
/// Máquina de estados conversacional (RF-15, menú numerado y pasos guiados). Pasajero: pedir transporte, ver, cancelar y
/// calificar; conductor: aceptar o rechazar asignaciones, iniciar y finalizar viajes y reportar novedades (RF-18). Solo
/// muestra y deja elegir lo que pertenece al usuario del número.
/// </summary>
public sealed class BotWhatsApp(
    ZonaHorariaOperador zona,
    IConsultasSolicitudes consultasSolicitudes,
    IConsultasAsignaciones consultasAsignaciones,
    IConsultasViajes consultasViajes,
    ICasoUsoConRetorno<RegistrarSolicitudDominio, SolicitudViaje> registrarSolicitud,
    ICasoUsoConRetorno<CancelarSolicitudDominio, SolicitudViaje> cancelarSolicitud,
    ICasoUsoConRetorno<CalificarViajeDominio, Calificacion> calificar,
    ICasoUsoConRetorno<AceptarAsignacionDominio, Viaje> aceptar,
    ICasoUsoConRetorno<RechazarAsignacionDominio, Asignacion> rechazar,
    ICasoUsoConRetorno<IniciarViajeDominio, Viaje> iniciar,
    ICasoUsoConRetorno<FinalizarViajeDominio, Viaje> finalizar,
    ICasoUsoConRetorno<ReportarIncidenteDominio, Incidente> reportarIncidente)
{
    /// <summary>Duración que se asume para una solicitud pedida por WhatsApp (el pasajero no la conoce).</summary>
    public static readonly TimeSpan DuracionPorDefecto = TimeSpan.FromMinutes(60);

    public const int MaximoPasajeros = 20;
    private const int MaximoOpciones = 5;

    private static readonly (string Texto, TipoIncidente Tipo)[] TiposNovedad =
    [
        ("Retraso", TipoIncidente.Retraso), ("Falla mecánica", TipoIncidente.FallaMecanica), ("Accidente", TipoIncidente.Accidente),
        ("Otra novedad", TipoIncidente.Otro)
    ];

    public async Task<DecisionBot> DecidirAsync(Conversacion conversacion, Usuario usuario, string tipo, string texto, DateTimeOffset ahora,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(conversacion);
        ArgumentNullException.ThrowIfNull(usuario);

        if (EntradaConversacion.PideMenu(texto) || conversacion.Expirada(ahora))
        {
            conversacion.Reiniciar(ahora);
            return new DecisionBot(Menu(usuario));
        }
        if (tipo != MensajeRecibidoTipos.Texto)
        {
            conversacion.Ir(conversacion.Paso, ahora);
            return new DecisionBot(TextosBot.SoloTexto);
        }

        return usuario switch
        {
            Pasajero pasajero => await PasajeroAsync(conversacion, pasajero, texto.Trim(), ahora, ct),
            Conductor conductor => await ConductorAsync(conversacion, conductor, texto.Trim(), ahora, ct),
            _ => new DecisionBot(TextosBot.NoRegistrado)
        };
    }

    private static string Menu(Usuario usuario) =>
        usuario is Conductor ? TextosBot.MenuConductor(usuario.Nombre) : TextosBot.MenuPasajero(usuario.Nombre);

    // ---------- Pasajero ----------

    private async Task<DecisionBot> PasajeroAsync(Conversacion c, Pasajero pasajero, string texto, DateTimeOffset ahora, CancellationToken ct)
    {
        switch (c.Paso)
        {
            case PasoConversacion.Menu:
                return EntradaConversacion.Numero(texto, 1, 4) switch
                {
                    1 => Paso(c, PasoConversacion.PedirOrigen, ahora, "¿Desde dónde te recogemos? Escribe la dirección."),
                    2 => await MisSolicitudesAsync(c, pasajero, ahora, ct),
                    3 => await ElegirCancelacionAsync(c, pasajero, ahora, ct),
                    4 => await ElegirCalificacionAsync(c, pasajero, ahora, ct),
                    _ => Inicio(c, pasajero, ahora)
                };

            case PasoConversacion.PedirOrigen:
                return Direccion(texto) is not { } origen
                    ? Repetir(c, ahora, "Escribe la dirección de recogida (al menos 3 caracteres).")
                    : Guardar(c, "origen", origen, PasoConversacion.PedirDestino, ahora, "¿A dónde vas? Escribe la dirección de destino.");

            case PasoConversacion.PedirDestino:
                return Direccion(texto) is not { } destino
                    ? Repetir(c, ahora, "Escribe la dirección de destino (al menos 3 caracteres).")
                    : Guardar(c, "destino", destino, PasoConversacion.PedirFechaHora, ahora,
                        "¿Para cuándo? Escribe la fecha y la hora, por ejemplo: *mañana 7:30*, *hoy 14:00* o *25/10 06:15*.");

            case PasoConversacion.PedirFechaHora:
                var hoy = DateOnly.FromDateTime(zona.Local(ahora).DateTime);
                if (EntradaConversacion.FechaHora(texto, hoy) is not { } fechaHora)
                    return Repetir(c, ahora, "No entendí la fecha. Escríbela así: *mañana 7:30*, *hoy 14:00* o *25/10 06:15*.");
                var servicio = zona.Instante(fechaHora.Fecha, fechaHora.Hora);
                if (servicio <= ahora)
                    return Repetir(c, ahora, "Esa hora ya pasó. Escribe una fecha y hora futuras.");
                return Guardar(c, "servicio", servicio.ToString("O", CultureInfo.InvariantCulture), PasoConversacion.PedirPasajeros, ahora,
                    $"¿Cuántas personas viajan? (1 a {MaximoPasajeros})");

            case PasoConversacion.PedirPasajeros:
                if (EntradaConversacion.Numero(texto, 1, MaximoPasajeros) is not { } cantidad)
                    return Repetir(c, ahora, $"Escribe un número de 1 a {MaximoPasajeros}.");
                c.Guardar("pasajeros", cantidad.ToString(CultureInfo.InvariantCulture));
                c.Ir(PasoConversacion.ConfirmarSolicitud, ahora);
                return new DecisionBot($"""
                    Revisa tu solicitud:
                    Recogida: {c.Requerido("origen")}
                    Destino: {c.Requerido("destino")}
                    Fecha y hora: {TextosBot.Fecha(zona.Local(Servicio(c)))}
                    Pasajeros: {cantidad}
                    1. Confirmar
                    2. Descartar
                    """);

            case PasoConversacion.ConfirmarSolicitud:
                switch (EntradaConversacion.Numero(texto, 1, 2))
                {
                    case 1:
                        var datos = new RegistrarSolicitudDominio(pasajero.Id, null, c.Requerido("origen"), c.Requerido("destino"), Servicio(c),
                            DuracionPorDefecto, int.Parse(c.Requerido("pasajeros"), CultureInfo.InvariantCulture), false, false,
                            CanalSolicitud.WhatsApp, pasajero.Id);
                        c.Reiniciar(ahora);
                        return new DecisionBot(string.Empty, async token =>
                        {
                            var solicitud = await registrarSolicitud.EjecutarAsync(datos, token);
                            return $"Listo, registramos tu solicitud para el {TextosBot.Fecha(zona.Local(solicitud.FechaHoraServicio))}. " +
                                   "Te avisaremos cuando tenga conductor. Puedes consultarla con la opción 2 del menú.";
                        });
                    case 2:
                        c.Reiniciar(ahora);
                        return new DecisionBot("Descartamos la solicitud. " + TextosBot.AyudaMenu);
                    default:
                        return Repetir(c, ahora, "Responde 1 para confirmar o 2 para descartar.");
                }

            case PasoConversacion.ElegirSolicitudCancelar:
                return Elegir(c, texto, ahora, "solicitud", PasoConversacion.MotivoCancelacion, "¿Por qué la cancelas? Escribe el motivo.");

            case PasoConversacion.MotivoCancelacion:
                if (Direccion(texto) is not { } motivo)
                    return Repetir(c, ahora, "Escribe el motivo de la cancelación.");
                var cancelacion = new CancelarSolicitudDominio(Guid.Parse(c.Requerido("solicitud")), motivo, pasajero.Id, false);
                c.Reiniciar(ahora);
                return new DecisionBot(string.Empty, async token =>
                {
                    await cancelarSolicitud.EjecutarAsync(cancelacion, token);
                    return "Cancelamos tu solicitud. " + TextosBot.AyudaMenu;
                });

            case PasoConversacion.ElegirViajeCalificar:
                return Elegir(c, texto, ahora, "viaje", PasoConversacion.PuntajeCalificacion,
                    "Del 1 al 5, ¿cómo calificas el servicio? (5 es excelente)");

            case PasoConversacion.PuntajeCalificacion:
                return EntradaConversacion.Numero(texto, Calificacion.PuntajeMinimo, Calificacion.PuntajeMaximo) is not { } puntaje
                    ? Repetir(c, ahora, "Escribe un número del 1 al 5.")
                    : Guardar(c, "puntaje", puntaje.ToString(CultureInfo.InvariantCulture), PasoConversacion.ComentarioCalificacion, ahora,
                        "¿Quieres dejar un comentario? Escríbelo, o responde 0 para terminar sin comentario.");

            case PasoConversacion.ComentarioCalificacion:
                var calificacion = new CalificarViajeDominio(Guid.Parse(c.Requerido("viaje")),
                    int.Parse(c.Requerido("puntaje"), CultureInfo.InvariantCulture), texto == "0" ? null : texto, pasajero.Id);
                c.Reiniciar(ahora);
                return new DecisionBot(string.Empty, async token =>
                {
                    await calificar.EjecutarAsync(calificacion, token);
                    return "¡Gracias por calificar el servicio! " + TextosBot.AyudaMenu;
                });

            default:
                return Inicio(c, pasajero, ahora);
        }
    }

    private async Task<DecisionBot> MisSolicitudesAsync(Conversacion c, Pasajero pasajero, DateTimeOffset ahora, CancellationToken ct)
    {
        c.Reiniciar(ahora);
        var solicitudes = (await consultasSolicitudes.BuscarAsync(new FiltroSolicitudes(PasajeroId: pasajero.Id), new Paginacion(1, MaximoOpciones), ct))
            .Elementos;
        if (solicitudes.Count == 0)
            return new DecisionBot("No tienes solicitudes. Responde 1 en el menú para pedir transporte. " + TextosBot.AyudaMenu);

        var lineas = new List<string>();
        foreach (var s in solicitudes)
        {
            var linea = $"{TextosBot.Fecha(zona.Local(s.FechaHoraServicio))} · {s.DireccionOrigen} → {s.DireccionDestino}: {TextosBot.Estado(s.Estado)}";
            if (s.Estado is EstadoSolicitud.Asignada or EstadoSolicitud.EnCurso
                && (await consultasAsignaciones.BuscarAsync(new FiltroAsignaciones(SolicitudId: s.Id), new Paginacion(1, 10), ct)).Elementos
                .FirstOrDefault(a => a.Estado is EstadoAsignacion.Asignada or EstadoAsignacion.Aceptada or EstadoAsignacion.EnCurso) is { } asignacion)
                linea += $" ({asignacion.ConductorNombre}, placa {asignacion.Placa})";
            lineas.Add(linea);
        }
        return new DecisionBot($"Tus solicitudes más recientes:\n{string.Join('\n', lineas.Select(l => "• " + l))}\n{TextosBot.AyudaMenu}");
    }

    private async Task<DecisionBot> ElegirCancelacionAsync(Conversacion c, Pasajero pasajero, DateTimeOffset ahora, CancellationToken ct)
    {
        var cancelables = (await consultasSolicitudes.BuscarAsync(new FiltroSolicitudes(PasajeroId: pasajero.Id), new Paginacion(1, 50), ct))
            .Elementos.Where(s => s.Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Asignada or EstadoSolicitud.SinCandidatos)
            .OrderBy(s => s.FechaHoraServicio).Take(MaximoOpciones).ToList();
        return Opciones(c, ahora, PasoConversacion.ElegirSolicitudCancelar, "¿Cuál solicitud quieres cancelar?",
            cancelables.Select(s => (s.Id, $"{TextosBot.Fecha(zona.Local(s.FechaHoraServicio))} · {s.DireccionOrigen} → {s.DireccionDestino}")).ToList(),
            "No tienes solicitudes que puedas cancelar.");
    }

    private async Task<DecisionBot> ElegirCalificacionAsync(Conversacion c, Pasajero pasajero, DateTimeOffset ahora, CancellationToken ct)
    {
        var sinCalificar = (await consultasViajes.BuscarAsync(new FiltroViajes(EstadoViaje.Finalizado, PasajeroId: pasajero.Id), new Paginacion(1, 50), ct))
            .Elementos.Where(v => v.Puntaje is null).Take(MaximoOpciones).ToList();
        return Opciones(c, ahora, PasoConversacion.ElegirViajeCalificar, "¿Qué viaje quieres calificar?",
            sinCalificar.Select(v => (v.Id, $"{TextosBot.Fecha(zona.Local(v.InicioProgramado))} · {v.DireccionOrigen} → {v.DireccionDestino} con {v.ConductorNombre}")).ToList(),
            "No tienes viajes terminados por calificar.");
    }

    // ---------- Conductor ----------

    private async Task<DecisionBot> ConductorAsync(Conversacion c, Conductor conductor, string texto, DateTimeOffset ahora, CancellationToken ct)
    {
        switch (c.Paso)
        {
            case PasoConversacion.Menu:
                return EntradaConversacion.Numero(texto, 1, 4) switch
                {
                    1 => await ElegirAsignacionAsync(c, conductor, ahora, ct),
                    2 => await ElegirViajeAsync(c, conductor, ahora, [EstadoViaje.Programado], PasoConversacion.ElegirViajeIniciar,
                        "¿Qué viaje vas a iniciar?", "No tienes viajes aceptados por iniciar.", ct),
                    3 => await ElegirViajeAsync(c, conductor, ahora, [EstadoViaje.EnCurso], PasoConversacion.ElegirViajeFinalizar,
                        "¿Qué viaje vas a finalizar?", "No tienes viajes en curso.", ct),
                    4 => await ElegirViajeAsync(c, conductor, ahora, [EstadoViaje.EnCurso, EstadoViaje.Programado], PasoConversacion.ElegirViajeNovedad,
                        "¿En qué viaje es la novedad?", "No tienes viajes en curso ni por iniciar.", ct),
                    _ => Inicio(c, conductor, ahora)
                };

            case PasoConversacion.ElegirAsignacion:
                return Elegir(c, texto, ahora, "asignacion", PasoConversacion.AceptarORechazar, "1. Aceptar\n2. Rechazar");

            case PasoConversacion.AceptarORechazar:
                var asignacionId = Guid.Parse(c.Requerido("asignacion"));
                switch (EntradaConversacion.Numero(texto, 1, 2))
                {
                    case 1:
                        c.Reiniciar(ahora);
                        return new DecisionBot(string.Empty, async token =>
                        {
                            await aceptar.EjecutarAsync(new AceptarAsignacionDominio(asignacionId, conductor.Id), token);
                            return "Aceptaste el servicio. Cuando empieces el recorrido, elige 2 en el menú para iniciar el viaje.";
                        });
                    case 2:
                        c.Ir(PasoConversacion.MotivoRechazo, ahora);
                        return new DecisionBot("¿Por qué lo rechazas? Escribe el motivo.");
                    default:
                        return Repetir(c, ahora, "Responde 1 para aceptar o 2 para rechazar.");
                }

            case PasoConversacion.MotivoRechazo:
                if (Direccion(texto) is not { } motivo)
                    return Repetir(c, ahora, "Escribe el motivo del rechazo.");
                var rechazo = new RechazarAsignacionDominio(Guid.Parse(c.Requerido("asignacion")), motivo, conductor.Id);
                c.Reiniciar(ahora);
                return new DecisionBot(string.Empty, async token =>
                {
                    await rechazar.EjecutarAsync(rechazo, token);
                    return "Registramos tu rechazo; el servicio se asignará a otro conductor. " + TextosBot.AyudaMenu;
                });

            case PasoConversacion.ElegirViajeIniciar:
                return Accion(c, texto, ahora, async (viajeId, token) =>
                {
                    await iniciar.EjecutarAsync(new IniciarViajeDominio(viajeId, conductor.Id), token);
                    return "Viaje iniciado. Buen recorrido. Al terminar, elige 3 en el menú.";
                });

            case PasoConversacion.ElegirViajeFinalizar:
                return Accion(c, texto, ahora, async (viajeId, token) =>
                {
                    await finalizar.EjecutarAsync(new FinalizarViajeDominio(viajeId, conductor.Id), token);
                    return "Viaje finalizado. ¡Gracias! " + TextosBot.AyudaMenu;
                });

            case PasoConversacion.ElegirViajeNovedad:
                return Elegir(c, texto, ahora, "viaje", PasoConversacion.TipoNovedad,
                    TextosBot.Lista("¿Qué pasó?", TiposNovedad.Select(t => t.Texto)));

            case PasoConversacion.TipoNovedad:
                return EntradaConversacion.Numero(texto, 1, TiposNovedad.Length) is not { } tipoNovedad
                    ? Repetir(c, ahora, TextosBot.OpcionInvalida)
                    : Guardar(c, "tipo", TiposNovedad[tipoNovedad - 1].Tipo.ToString(), PasoConversacion.DescripcionNovedad, ahora,
                        "Cuéntanos qué pasó en un mensaje.");

            case PasoConversacion.DescripcionNovedad:
                if (Direccion(texto) is not { } descripcion)
                    return Repetir(c, ahora, "Describe la novedad en un mensaje.");
                var novedad = new ReportarIncidenteDominio(Guid.Parse(c.Requerido("viaje")), Enum.Parse<TipoIncidente>(c.Requerido("tipo")),
                    descripcion, conductor.Id);
                c.Reiniciar(ahora);
                return new DecisionBot(string.Empty, async token =>
                {
                    await reportarIncidente.EjecutarAsync(novedad, token);
                    return "Registramos la novedad y avisamos al operador. " + TextosBot.AyudaMenu;
                });

            default:
                return Inicio(c, conductor, ahora);
        }
    }

    private async Task<DecisionBot> ElegirAsignacionAsync(Conversacion c, Conductor conductor, DateTimeOffset ahora, CancellationToken ct)
    {
        var pendientes = (await consultasAsignaciones.BuscarAsync(new FiltroAsignaciones(conductor.Id, Estado: EstadoAsignacion.Asignada),
                new Paginacion(1, MaximoOpciones), ct))
            .Elementos.OrderBy(a => a.Inicio).ToList();
        var opciones = new List<(Guid, string)>();
        foreach (var asignacion in pendientes)
        {
            var solicitud = (await consultasSolicitudes.DetalleAsync(asignacion.SolicitudId, ct))?.Solicitud;
            opciones.Add((asignacion.Id, $"{TextosBot.Fecha(zona.Local(asignacion.Inicio))} · {solicitud?.DireccionOrigen} → {solicitud?.DireccionDestino}" +
                                         $" · {solicitud?.CantidadPasajeros} pasajero(s) · placa {asignacion.Placa}"));
        }
        return Opciones(c, ahora, PasoConversacion.ElegirAsignacion, "Tienes estos servicios por responder:", opciones,
            "No tienes asignaciones por responder.");
    }

    private async Task<DecisionBot> ElegirViajeAsync(Conversacion c, Conductor conductor, DateTimeOffset ahora, EstadoViaje[] estados,
        PasoConversacion paso, string titulo, string sinOpciones, CancellationToken ct)
    {
        var viajes = new List<ViajeLectura>();
        foreach (var estado in estados)
            viajes.AddRange((await consultasViajes.BuscarAsync(new FiltroViajes(estado, conductor.Id), new Paginacion(1, MaximoOpciones), ct)).Elementos);
        return Opciones(c, ahora, paso, titulo,
            viajes.OrderBy(v => v.InicioProgramado).Take(MaximoOpciones)
                .Select(v => (v.Id, $"{TextosBot.Fecha(zona.Local(v.InicioProgramado))} · {v.DireccionOrigen} → {v.DireccionDestino} · {v.PasajeroNombre}"))
                .ToList(),
            sinOpciones);
    }

    // ---------- Piezas comunes ----------

    private static DecisionBot Inicio(Conversacion c, Usuario usuario, DateTimeOffset ahora)
    {
        c.Reiniciar(ahora);
        return new DecisionBot(Menu(usuario));
    }

    private static DecisionBot Paso(Conversacion c, PasoConversacion paso, DateTimeOffset ahora, string pregunta)
    {
        c.Ir(paso, ahora);
        return new DecisionBot(pregunta);
    }

    private static DecisionBot Repetir(Conversacion c, DateTimeOffset ahora, string ayuda)
    {
        c.Ir(c.Paso, ahora);
        return new DecisionBot(ayuda);
    }

    private static DecisionBot Guardar(Conversacion c, string clave, string valor, PasoConversacion siguiente, DateTimeOffset ahora, string pregunta)
    {
        c.Guardar(clave, valor);
        return Paso(c, siguiente, ahora, pregunta);
    }

    private static DecisionBot Opciones(Conversacion c, DateTimeOffset ahora, PasoConversacion paso, string titulo,
        IReadOnlyList<(Guid Id, string Texto)> opciones, string sinOpciones)
    {
        if (opciones.Count == 0)
        {
            c.Reiniciar(ahora);
            return new DecisionBot($"{sinOpciones} {TextosBot.AyudaMenu}");
        }
        c.GuardarOpciones(opciones.Select(o => o.Id).ToList());
        return Paso(c, paso, ahora, TextosBot.Lista(titulo, opciones.Select(o => o.Texto)));
    }

    /// <summary>Traduce el número elegido a la opción mostrada y la guarda.</summary>
    private static DecisionBot Elegir(Conversacion c, string texto, DateTimeOffset ahora, string clave, PasoConversacion siguiente, string pregunta) =>
        EntradaConversacion.Numero(texto, 1, MaximoOpciones) is { } numero && c.Opcion(numero) is { } id
            ? Guardar(c, clave, id.ToString(), siguiente, ahora, pregunta)
            : Repetir(c, ahora, TextosBot.OpcionInvalida);

    /// <summary>Elegir una opción termina el flujo con una acción sobre ella.</summary>
    private static DecisionBot Accion(Conversacion c, string texto, DateTimeOffset ahora, Func<Guid, CancellationToken, Task<string>> accion)
    {
        if (EntradaConversacion.Numero(texto, 1, MaximoOpciones) is not { } numero || c.Opcion(numero) is not { } id)
            return Repetir(c, ahora, TextosBot.OpcionInvalida);
        c.Reiniciar(ahora);
        return new DecisionBot(string.Empty, token => accion(id, token));
    }

    private static string? Direccion(string texto) => texto.Trim().Length >= 3 ? texto.Trim() : null;

    private static DateTimeOffset Servicio(Conversacion c) =>
        DateTimeOffset.Parse(c.Requerido("servicio"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}

/// <summary>Tipos de mensaje que entiende el bot.</summary>
internal static class MensajeRecibidoTipos
{
    public const string Texto = Domain.Canal.GestionCanal.MensajeRecibido.TipoTexto;
}
