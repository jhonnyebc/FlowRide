using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Seguridad.Salida.Puertos;

namespace FlowRide.Application.Seguridad.CasosUso;

/// <summary>Sin parámetros: todo el Silo del operador.</summary>
public sealed record AsegurarCifradoDominio
{
    public static AsegurarCifradoDominio Silo { get; } = new();
}

/// <summary>
/// Paso de mantenimiento del aprovisionamiento: deja los campos sensibles cifrados con la clave vigente (ADR-0014).
/// Cubre los datos anteriores a la Fase 3 y la rotación de claves.
/// </summary>
public sealed class AsegurarCifradoCasoUso(IRecifradorCampos recifrador) : ICasoUsoConRetorno<AsegurarCifradoDominio, int>
{
    public Task<int> EjecutarAsync(AsegurarCifradoDominio datos, CancellationToken ct) => recifrador.RecifrarAsync(ct);
}
