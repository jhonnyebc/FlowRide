using System.Text.Json;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Parametros.Entrada.Dto;
using FlowRide.Application.Parametros.Entrada.Puertos;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Parametros.Entrada.Adaptadores;

public sealed class ParametrosAdaptadorEntrada(
    ICasoUsoConRetorno<ConsultarParametrosDominio, IReadOnlyList<ParametroVigente>> consultar,
    ICasoUsoConRetorno<CambiarParametroDominio, ParametroVigente> cambiar) : IParametrosPuertoEntrada
{
    public async Task<IReadOnlyList<ParametroRespuesta>> ConsultarAsync(CancellationToken ct) =>
        (await consultar.EjecutarAsync(ConsultarParametrosDominio.Todos, ct)).Select(ARespuesta).ToList();

    public async Task<ParametroRespuesta> CambiarAsync(string clave, CambiarParametroPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (peticion?.Valor is not { } valor || valor.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new FlowRideExcepcion("valor es obligatorio.");
        return ARespuesta(await cambiar.EjecutarAsync(new CambiarParametroDominio(clave, valor.GetRawText(), actorId), ct));
    }

    private static ParametroRespuesta ARespuesta(ParametroVigente vigente)
    {
        var definicion = vigente.Definicion;
        var esEntero = definicion.Tipo == TipoParametro.Entero;
        return new ParametroRespuesta(
            definicion.Clave,
            definicion.Tipo.ToString(),
            definicion.Descripcion,
            JsonDocument.Parse(vigente.Valor).RootElement.Clone(),
            JsonDocument.Parse(definicion.ValorPorDefecto).RootElement.Clone(),
            vigente.Version,
            vigente.EsPorDefecto,
            vigente.Guardado?.ModificadoEn,
            esEntero ? definicion.Minimo : null,
            esEntero ? definicion.Maximo : null,
            definicion.Opciones);
    }
}
