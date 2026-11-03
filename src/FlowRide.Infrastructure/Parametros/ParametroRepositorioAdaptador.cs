using FlowRide.Application.Parametros.Salida.Puertos;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Parametros.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FlowRide.Infrastructure.Parametros;

/// <summary>Opciones de la caché de parámetros. El TTL nunca supera 60 s (ADR-0006, ADR-0016).</summary>
public sealed class CacheParametrosOptions
{
    public const string Seccion = "FlowRide:Cache";
    public static readonly TimeSpan TtlMaximo = TimeSpan.FromSeconds(60);

    public int TtlParametrosSegundos { get; set; } = 60;

    public TimeSpan Ttl => TimeSpan.FromSeconds(Math.Clamp(TtlParametrosSegundos, 1, (int)TtlMaximo.TotalSeconds));
}

/// <summary>
/// Parámetros del Silo con cache-aside (ADR-0006): la lista completa se cachea en memoria con TTL ≤ 60 s y se invalida
/// después de confirmar un cambio. Consultar uno por clave (para modificarlo) siempre va a la base.
/// </summary>
public sealed class ParametroRepositorioAdaptador(FlowRideDbContext contexto, IMemoryCache cache, CacheParametrosOptions opciones)
    : IParametroRepositorio, ICacheParametros
{
    internal const string ClaveCache = "parametros:todos";

    public Task CrearAsync(Parametro entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Parametros.Add(entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(string id, Parametro entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (contexto.Entry(entidad).State == EntityState.Detached)
            contexto.Parametros.Update(entidad);
        return Task.CompletedTask;
    }

    public Task<Parametro?> ConsultarPorIdAsync(string id, CancellationToken ct) =>
        contexto.Parametros.SingleOrDefaultAsync(parametro => parametro.Clave == id, ct);

    public async Task<IReadOnlyList<Parametro>> ConsultarTodosAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveCache, out IReadOnlyList<Parametro>? enCache) && enCache is not null)
            return enCache;

        IReadOnlyList<Parametro> parametros = await contexto.Parametros.AsNoTracking().OrderBy(p => p.Clave).ToListAsync(ct);
        cache.Set(ClaveCache, parametros, opciones.Ttl);
        return parametros;
    }

    public void Invalidar() => cache.Remove(ClaveCache);
}
