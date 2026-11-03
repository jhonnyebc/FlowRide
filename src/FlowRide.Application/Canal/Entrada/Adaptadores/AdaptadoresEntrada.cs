using FlowRide.Application.Canal.CasosUso;
using FlowRide.Application.Canal.Entrada.Dto;
using FlowRide.Application.Canal.Entrada.Puertos;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Canal.GestionCanal;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Canal.Entrada.Adaptadores;

public sealed class CanalWhatsAppAdaptadorEntrada(
    IWhatsAppGateway gateway,
    ICasoUsoConRetorno<RecibirWebhookDominio, ResultadoWebhook> recibir,
    ICasoUsoConRetorno<SimularMensajeDominio, bool> simular,
    ICasoUsoConRetorno<ConsultarEntrantesPendientesDominio, IReadOnlyList<Guid>> entrantes,
    ICasoUsoConRetorno<ProcesarMensajeEntranteDominio, bool> procesar,
    ICasoUsoConRetorno<RegistrarFalloEntranteDominio, bool> fallo,
    ICasoUsoConRetorno<ConsultarSalientesPendientesDominio, IReadOnlyList<Guid>> salientes,
    ICasoUsoConRetorno<EnviarMensajeSalienteDominio, TipoResultadoEnvio?> enviar,
    ICasoUsoConRetorno<ConsultarConversacionCanalDominio, IReadOnlyList<MensajeCanalLectura>> conversacion) : ICanalWhatsAppPuertoEntrada
{
    public bool VerificarSuscripcion(string? modo, string? token) => gateway.VerificarSuscripcion(modo, token);

    public async Task<WebhookRespuesta> RecibirWebhookAsync(string cuerpo, string? firma, CancellationToken ct)
    {
        var r = await recibir.EjecutarAsync(new RecibirWebhookDominio(cuerpo ?? string.Empty, firma), ct);
        return new WebhookRespuesta(r.FirmaValida, r.FormatoValido, r.Nuevos, r.Duplicados, r.EstadosActualizados, r.Ignorados);
    }

    public async Task SimularMensajeAsync(MensajeSimuladoPeticion peticion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Telefono) || string.IsNullOrWhiteSpace(peticion.Texto))
            throw new FlowRideExcepcion("telefono y texto son obligatorios.");
        await simular.EjecutarAsync(new SimularMensajeDominio(peticion.Telefono, peticion.Texto), ct);
    }

    public Task<IReadOnlyList<Guid>> EntrantesPendientesAsync(int maximo, CancellationToken ct) =>
        entrantes.EjecutarAsync(new ConsultarEntrantesPendientesDominio(maximo), ct);

    public Task<bool> ProcesarEntranteAsync(Guid mensajeId, CancellationToken ct) =>
        procesar.EjecutarAsync(new ProcesarMensajeEntranteDominio(mensajeId), ct);

    public Task<bool> RegistrarFalloEntranteAsync(Guid mensajeId, string error, CancellationToken ct) =>
        fallo.EjecutarAsync(new RegistrarFalloEntranteDominio(mensajeId, error), ct);

    public Task<IReadOnlyList<Guid>> SalientesPendientesAsync(int maximo, CancellationToken ct) =>
        salientes.EjecutarAsync(new ConsultarSalientesPendientesDominio(maximo), ct);

    public async Task<string?> EnviarSalienteAsync(Guid mensajeId, CancellationToken ct) =>
        (await enviar.EjecutarAsync(new EnviarMensajeSalienteDominio(mensajeId), ct))?.ToString();

    public Task<IReadOnlyList<MensajeCanalLectura>> ConversacionAsync(string telefono, CancellationToken ct) =>
        conversacion.EjecutarAsync(new ConsultarConversacionCanalDominio(telefono), ct);
}
