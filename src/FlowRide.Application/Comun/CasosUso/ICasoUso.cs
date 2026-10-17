namespace FlowRide.Application.Comun.CasosUso;

/// <summary>Caso de uso que recibe los datos del dominio y devuelve un resultado.</summary>
public interface ICasoUsoConRetorno<in TEntrada, TSalida>
{
    Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct);
}

/// <summary>Caso de uso que recibe los datos del dominio y no devuelve nada.</summary>
public interface ICasoUsoSinRetorno<in TEntrada>
{
    Task EjecutarAsync(TEntrada datos, CancellationToken ct);
}
