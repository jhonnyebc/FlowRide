using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Seguridad.CasosUso;
using FlowRide.Application.Seguridad.Entrada.Puertos;

namespace FlowRide.Application.Seguridad.Entrada.Adaptadores;

public sealed class AsegurarCifradoAdaptadorEntrada(ICasoUsoConRetorno<AsegurarCifradoDominio, int> casoUso)
    : IAsegurarCifradoPuertoEntrada
{
    public Task<int> AsegurarAsync(CancellationToken ct) => casoUso.EjecutarAsync(AsegurarCifradoDominio.Silo, ct);
}
