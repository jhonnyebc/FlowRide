using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Repositorios;

namespace FlowRide.Application.Tenancy.CasosUso;

/// <summary>Operadores del catálogo, en orden por Id; con un estado, solo los que están en él.</summary>
public sealed class ConsultarOperadoresCasoUso(IOperadorRepositorio operadores)
    : ICasoUsoConRetorno<EstadoOperador?, IReadOnlyList<Operador>>
{
    public async Task<IReadOnlyList<Operador>> EjecutarAsync(EstadoOperador? estado, CancellationToken ct) =>
        (await operadores.ConsultarTodosAsync(ct))
        .Where(operador => estado is null || operador.Estado == estado)
        .OrderBy(operador => operador.Id, StringComparer.Ordinal)
        .ToList();
}
