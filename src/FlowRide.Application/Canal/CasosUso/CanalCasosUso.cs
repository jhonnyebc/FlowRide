using FlowRide.Application.Canal.Bot;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.GestionCanal;
using FlowRide.Domain.Canal.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Canal.CasosUso;

/// <summary>Resultado de recibir un webhook: el adaptador HTTP responde 401 si la firma no es válida y 400 si no se entiende.</summary>
public sealed record ResultadoWebhook(bool FirmaValida, bool FormatoValido, int Nuevos, int Duplicados, int EstadosActualizados, int Ignorados)
{
    public static ResultadoWebhook FirmaInvalida { get; } = new(false, false, 0, 0, 0, 0);
    public static ResultadoWebhook Malformado { get; } = new(true, false, 0, 0, 0, 0);
}

/// <summary>Guarda los mensajes entrantes nuevos; los repetidos (mismo Id externo) se cuentan y se ignoran (PoC 4).</summary>
public sealed class RegistroEntrantes(IMensajeEntranteRepositorio entrantes, TimeProvider reloj)
{
    public async Task<(int Nuevos, int Duplicados)> RegistrarAsync(IEnumerable<MensajeRecibido> mensajes, CancellationToken ct)
    {
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var (nuevos, duplicados) = (0, 0);
        foreach (var mensaje in mensajes)
        {
            if (!vistos.Add(mensaje.IdExterno) || await entrantes.ExisteAsync(mensaje.IdExterno, ct))
            {
                duplicados++;
                continue;
            }
            await entrantes.CrearAsync(new MensajeEntrante(Guid.CreateVersion7(reloj.GetUtcNow()), mensaje.IdExterno, mensaje.Telefono,
                mensaje.Tipo, mensaje.Contenido, mensaje.RecibidoEn), ct);
            nuevos++;
        }
        return (nuevos, duplicados);
    }
}

/// <summary>
/// Webhook del canal (RF-14, ADR-0008): valida la firma, normaliza, guarda los mensajes nuevos (idempotente por su Id
/// externo) y los estados de entrega de lo enviado, y responde de inmediato. El procesamiento va aparte (worker).
/// </summary>
public sealed class RecibirWebhookCasoUso(
    IWhatsAppGateway gateway,
    RegistroEntrantes registro,
    IMensajeSalienteRepositorio salientes,
    IUnidadTrabajo unidadTrabajo) : ICasoUsoConRetorno<RecibirWebhookDominio, ResultadoWebhook>
{
    public async Task<ResultadoWebhook> EjecutarAsync(RecibirWebhookDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (!gateway.FirmaValida(datos.Cuerpo, datos.Firma))
            return ResultadoWebhook.FirmaInvalida;

        WebhookTraducido webhook;
        try
        {
            webhook = gateway.Traducir(datos.Cuerpo);
        }
        catch (FormatException)
        {
            return ResultadoWebhook.Malformado;
        }

        var (nuevos, duplicados) = await registro.RegistrarAsync(webhook.Mensajes, ct);
        var actualizados = 0;
        foreach (var estado in webhook.Estados)
        {
            if (await salientes.ConsultarPorIdExternoAsync(estado.IdExterno, ct) is not { } saliente)
                continue;
            saliente.RegistrarEstadoExterno(estado.Estado, estado.Error);
            actualizados++;
        }

        await unidadTrabajo.GuardarCambiosAsync(ct);
        return new ResultadoWebhook(true, true, nuevos, duplicados, actualizados, webhook.Ignorados);
    }
}

/// <summary>Mensaje inyectado en desarrollo con el proveedor simulado: entra a la misma bandeja que el webhook.</summary>
public sealed class SimularMensajeCasoUso(RegistroEntrantes registro, IUnidadTrabajo unidadTrabajo, TimeProvider reloj)
    : ICasoUsoConRetorno<SimularMensajeDominio, bool>
{
    public async Task<bool> EjecutarAsync(SimularMensajeDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var mensaje = new MensajeRecibido($"sim-{Guid.CreateVersion7(ahora)}", EntradaConversacion.TelefonoE164(datos.Telefono),
            MensajeRecibido.TipoTexto, datos.Texto ?? string.Empty, ahora);
        var (nuevos, _) = await registro.RegistrarAsync([mensaje], ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return nuevos == 1;
    }
}

public sealed class ConsultarEntrantesPendientesCasoUso(IMensajeEntranteRepositorio entrantes)
    : ICasoUsoConRetorno<ConsultarEntrantesPendientesDominio, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> EjecutarAsync(ConsultarEntrantesPendientesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return entrantes.ConsultarPendientesAsync(Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

public sealed class ConsultarSalientesPendientesCasoUso(IMensajeSalienteRepositorio salientes, TimeProvider reloj)
    : ICasoUsoConRetorno<ConsultarSalientesPendientesDominio, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> EjecutarAsync(ConsultarSalientesPendientesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return salientes.ConsultarPendientesAsync(reloj.GetUtcNow(), Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

/// <summary>
/// Procesa un mensaje entrante con el bot (RF-15, RF-18): identifica al usuario por su número (COND-SEG: solo actúa el
/// número registrado), avanza su conversación, ejecuta la acción de negocio si el paso la cierra y deja la respuesta en la
/// bandeja de salida. El mensaje se marca procesado en la misma transacción que la acción, así no se ejecuta dos veces.
/// </summary>
public sealed class ProcesarMensajeEntranteCasoUso(
    IMensajeEntranteRepositorio entrantes,
    IMensajeSalienteRepositorio salientes,
    IConversacionRepositorio conversaciones,
    IUsuarioRepositorio usuarios,
    BotWhatsApp bot,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ProcesarMensajeEntranteDominio, bool>
{
    public async Task<bool> EjecutarAsync(ProcesarMensajeEntranteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (await entrantes.ConsultarPorIdAsync(datos.MensajeId, ct) is not { Estado: EstadoMensajeEntrante.Pendiente } mensaje)
            return false;

        var ahora = reloj.GetUtcNow();
        var usuario = await usuarios.ConsultarPorTelefonoAsync(mensaje.Remitente, ct);
        mensaje.MarcarProcesado(usuario?.Id, ahora);

        string respuesta;
        if (usuario is not (Pasajero or Conductor))
            respuesta = TextosBot.NoRegistrado;
        else if (!usuario.Activo)
            respuesta = TextosBot.Inactivo;
        else
        {
            var conversacion = await ConversacionAsync(mensaje.Remitente, ahora, ct);
            var decision = await bot.DecidirAsync(conversacion, usuario, mensaje.Tipo, mensaje.Contenido, ahora, ct);
            respuesta = decision.Respuesta;
            if (decision.Accion is { } accion)
            {
                try
                {
                    // La acción confirma junto con el mensaje marcado y la conversación avanzada.
                    respuesta = await accion(ct);
                }
                catch (FlowRideExcepcion error)
                {
                    unidadTrabajo.DescartarCambios();
                    mensaje = (await entrantes.ConsultarPorIdAsync(datos.MensajeId, ct))!;
                    if (mensaje.Estado == EstadoMensajeEntrante.Pendiente)
                        mensaje.MarcarProcesado(usuario.Id, ahora);
                    (await ConversacionAsync(mensaje.Remitente, ahora, ct)).Reiniciar(ahora);
                    respuesta = TextosBot.NoSePudo(error.Message);
                }
            }
        }

        await salientes.CrearAsync(new MensajeSaliente(Guid.CreateVersion7(ahora), mensaje.Remitente, respuesta, ahora, mensaje.Id), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return true;
    }

    private async Task<Conversacion> ConversacionAsync(string telefono, DateTimeOffset ahora, CancellationToken ct)
    {
        if (await conversaciones.ConsultarPorTelefonoAsync(telefono, ct) is { } existente)
            return existente;
        var nueva = new Conversacion(Guid.CreateVersion7(ahora), telefono, ahora);
        await conversaciones.CrearAsync(nueva, ct);
        return nueva;
    }
}

/// <summary>Un error técnico al procesar un mensaje: cuenta el intento; al quinto queda Fallido.</summary>
public sealed class RegistrarFalloEntranteCasoUso(IMensajeEntranteRepositorio entrantes, IUnidadTrabajo unidadTrabajo)
    : ICasoUsoConRetorno<RegistrarFalloEntranteDominio, bool>
{
    public async Task<bool> EjecutarAsync(RegistrarFalloEntranteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (await entrantes.ConsultarPorIdAsync(datos.MensajeId, ct) is not { } mensaje)
            return false;
        mensaje.RegistrarFallo(datos.Error);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return mensaje.Estado == EstadoMensajeEntrante.Fallido;
    }
}

/// <summary>
/// Envía un mensaje de la bandeja de salida (RF-16): guarda el Id del proveedor (correlación); un error transitorio se
/// reintenta con espera creciente y uno permanente queda registrado sin reintentos (ADR-0008).
/// </summary>
public sealed class EnviarMensajeSalienteCasoUso(
    IMensajeSalienteRepositorio salientes,
    IWhatsAppGateway gateway,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<EnviarMensajeSalienteDominio, TipoResultadoEnvio?>
{
    public async Task<TipoResultadoEnvio?> EjecutarAsync(EnviarMensajeSalienteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        if (await salientes.ConsultarPorIdAsync(datos.MensajeId, ct) is not { } mensaje || !mensaje.ListoParaEnviar(ahora))
            return null;

        var resultado = await gateway.EnviarTextoAsync(mensaje.Destinatario, mensaje.Contenido, ct);
        switch (resultado.Tipo)
        {
            case TipoResultadoEnvio.Enviado:
                mensaje.MarcarEnviado(resultado.IdExterno!, reloj.GetUtcNow());
                break;
            case TipoResultadoEnvio.ErrorTransitorio:
                mensaje.ProgramarReintento(resultado.Error ?? "Error transitorio del proveedor.", reloj.GetUtcNow());
                break;
            default:
                mensaje.MarcarFallido(resultado.Error ?? "Error permanente del proveedor.");
                break;
        }

        await unidadTrabajo.GuardarCambiosAsync(ct);
        return resultado.Tipo;
    }
}

/// <summary>La conversación de un número con el bot, para el panel (soporte y trazabilidad del canal).</summary>
public sealed class ConsultarConversacionCanalCasoUso(IConsultasCanal consultas)
    : ICasoUsoConRetorno<ConsultarConversacionCanalDominio, IReadOnlyList<MensajeCanalLectura>>
{
    public const int MaximoMensajes = 100;

    public Task<IReadOnlyList<MensajeCanalLectura>> EjecutarAsync(ConsultarConversacionCanalDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (string.IsNullOrWhiteSpace(datos.Telefono))
            throw new FlowRideExcepcion("telefono es obligatorio.");
        return consultas.ConversacionAsync(EntradaConversacion.TelefonoE164(datos.Telefono), MaximoMensajes, ct);
    }
}
