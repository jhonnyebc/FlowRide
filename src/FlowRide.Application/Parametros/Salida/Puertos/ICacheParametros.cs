namespace FlowRide.Application.Parametros.Salida.Puertos;

/// <summary>
/// Caché local de los parámetros (cache-aside, TTL ≤ 60 s, ADR-0006). Se invalida después de confirmar un cambio:
/// la réplica que lo hizo lo ve de inmediato y las demás, al vencer el TTL.
/// </summary>
public interface ICacheParametros
{
    void Invalidar();
}
