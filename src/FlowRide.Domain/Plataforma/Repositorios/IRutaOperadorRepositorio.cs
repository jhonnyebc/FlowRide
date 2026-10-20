using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Plataforma.Repositorios;

/// <summary>Rutas del borde en el catálogo de plataforma (ADR-0012); el Id es el del operador.</summary>
public interface IRutaOperadorRepositorio :
    ICrearRepositorio<RutaOperador>,
    IConsultarRepositorio<string, RutaOperador>,
    IModificarRepositorio<string, RutaOperador>
{
    Task<RutaOperador?> ConsultarPorSubdominioAsync(string subdominio, CancellationToken ct);

    Task<RutaOperador?> ConsultarPorNumeroAsync(string phoneNumberId, CancellationToken ct);

    /// <summary>Rutas de los operadores en estado Activo (las que publica el borde).</summary>
    Task<IReadOnlyList<RutaOperador>> ConsultarDeOperadoresActivosAsync(CancellationToken ct);
}
