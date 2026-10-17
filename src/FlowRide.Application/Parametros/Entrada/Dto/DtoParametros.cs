using System.Text.Json;

namespace FlowRide.Application.Parametros.Entrada.Dto;

/// <summary>Nuevo valor en JSON: un número, un texto o una lista de franjas, según el tipo del parámetro.</summary>
public sealed record CambiarParametroPeticion
{
    public JsonElement? Valor { get; init; }
}

public sealed record ParametroRespuesta(
    string Clave,
    string Tipo,
    string Descripcion,
    JsonElement Valor,
    JsonElement ValorPorDefecto,
    int Version,
    bool EsPorDefecto,
    DateTimeOffset? ModificadoEn,
    int? Minimo,
    int? Maximo,
    IReadOnlyList<string> Opciones);
