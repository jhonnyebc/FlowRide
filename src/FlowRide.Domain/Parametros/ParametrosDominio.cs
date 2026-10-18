using System.Text.Json;
using FlowRide.Domain.Parametros.Repositorios;

namespace FlowRide.Domain.Parametros;

/// <summary>
/// Lectura tipada de los parámetros que rigen para el operador (ADR-0016): el valor guardado o, si no hay, el de producto.
/// La lista viene de la caché del repositorio (TTL ≤ 60 s).
/// </summary>
public sealed class ParametrosDominio(IParametroRepositorio parametros)
{
    public async Task<int> EnteroAsync(string clave, CancellationToken ct) =>
        JsonSerializer.Deserialize<int>(await ValorAsync(clave, ct));

    public async Task<string> OpcionAsync(string clave, CancellationToken ct) =>
        JsonSerializer.Deserialize<string>(await ValorAsync(clave, ct))!;

    private async Task<string> ValorAsync(string clave, CancellationToken ct)
    {
        var definicion = CatalogoParametros.Buscar(clave);
        var guardado = (await parametros.ConsultarTodosAsync(ct)).FirstOrDefault(p => p.Clave == definicion.Clave);
        return guardado?.Valor ?? definicion.ValorPorDefecto;
    }
}
