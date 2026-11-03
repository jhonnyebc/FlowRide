using FlowRide.Application.Borde.Entrada.Dto;
using FlowRide.Application.Borde.Entrada.Puertos;
using FlowRide.Application.Borde.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Borde;

namespace FlowRide.Application.Borde.Entrada.Adaptadores;

public sealed class BordeAdaptadorEntrada(
    ICasoUsoConRetorno<CargarTablaRutasDominio, TablaRutas> cargar,
    ICasoUsoConRetorno<EnrutarWebhookDominio, ResultadoEnrutamientoWebhook> enrutar,
    IVerificadorWebhook verificador) : IBordePuertoEntrada
{
    public async Task<RutasVigentes> CargarRutasAsync(string dominioBase, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominioBase);
        return new RutasVigentes(await cargar.EjecutarAsync(CargarTablaRutasDominio.Instancia, ct), dominioBase);
    }

    public bool VerificarSuscripcion(string? modo, string? token) => verificador.VerificarSuscripcion(modo, token);

    public async Task<WebhookEnrutadoRespuesta> EnrutarWebhookAsync(string cuerpo, string? firma, RutasVigentes rutas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rutas);
        var resultado = await enrutar.EjecutarAsync(new EnrutarWebhookDominio(cuerpo ?? string.Empty, firma, rutas.Tabla), ct);
        return new WebhookEnrutadoRespuesta(resultado.FirmaValida, resultado.FormatoValido,
            resultado.Destinos.Select(r => new DestinoWebhook(r.OperadorId, new Uri(r.UrlStack))).ToList(), resultado.NumerosSinOperador);
    }
}
