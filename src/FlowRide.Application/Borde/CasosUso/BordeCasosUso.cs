using FlowRide.Application.Borde.Salida.Puertos;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Borde;
using FlowRide.Domain.Plataforma.Repositorios;

namespace FlowRide.Application.Borde.CasosUso;

/// <summary>Tabla de ruteo con las rutas de los operadores activos (un operador suspendido deja de ser alcanzable).</summary>
public sealed class CargarTablaRutasCasoUso(IRutaOperadorRepositorio rutas) : ICasoUsoConRetorno<CargarTablaRutasDominio, TablaRutas>
{
    public async Task<TablaRutas> EjecutarAsync(CargarTablaRutasDominio datos, CancellationToken ct) =>
        new(await rutas.ConsultarDeOperadoresActivosAsync(ct));
}

/// <summary>
/// Router de webhooks (ADR-0012): valida la firma con el secreto de la app de Meta, lee los <c>phone_number_id</c> y
/// devuelve los stacks a los que se reenvía el cuerpo intacto. No guarda nada; cada stack vuelve a validar la firma
/// (defensa en profundidad, decisión del 2026-10-19) e ignora lo que no es de su número.
/// </summary>
public sealed class EnrutarWebhookCasoUso(IVerificadorWebhook verificador) : ICasoUsoConRetorno<EnrutarWebhookDominio, ResultadoEnrutamientoWebhook>
{
    public Task<ResultadoEnrutamientoWebhook> EjecutarAsync(EnrutarWebhookDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (!verificador.FirmaValida(datos.Cuerpo, datos.Firma))
            return Task.FromResult(ResultadoEnrutamientoWebhook.FirmaInvalida);

        IReadOnlyList<string> numeros;
        try
        {
            numeros = verificador.NumerosDestino(datos.Cuerpo);
        }
        catch (FormatException)
        {
            return Task.FromResult(ResultadoEnrutamientoWebhook.FormatoInvalido);
        }

        var destinos = new List<RutaOperador>();
        var sinOperador = new List<string>();
        foreach (var numero in numeros.Distinct(StringComparer.Ordinal))
        {
            if (datos.Tabla.PorNumero(numero) is { } ruta)
            {
                if (!destinos.Contains(ruta)) destinos.Add(ruta);
            }
            else
            {
                sinOperador.Add(numero);
            }
        }
        return Task.FromResult(new ResultadoEnrutamientoWebhook(true, true, destinos, sinOperador));
    }
}
