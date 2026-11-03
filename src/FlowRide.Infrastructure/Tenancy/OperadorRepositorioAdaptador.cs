using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>
/// Adaptador de salida del repositorio de operadores sobre el catálogo de plataforma (EF Core).
/// EF mapea la entidad del dominio directamente con configuración fluida: no hay modelo de persistencia aparte.
/// </summary>
public sealed class OperadorRepositorioAdaptador(CatalogoDbContext catalogo) : IOperadorRepositorio
{
    public async Task CrearAsync(Operador entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        catalogo.Operadores.Add(entidad);
        await catalogo.SaveChangesAsync(ct);
    }

    public async Task ModificarAsync(string id, Operador entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (entidad.Id != id)
            throw new ArgumentException($"El Id '{id}' no coincide con el del operador '{entidad.Id}'.", nameof(id));

        // Entidades leídas en este contexto ya están rastreadas; las que llegan de fuera se adjuntan como modificadas.
        if (catalogo.Entry(entidad).State == EntityState.Detached)
            catalogo.Operadores.Update(entidad);

        await catalogo.SaveChangesAsync(ct);
    }

    public Task<Operador?> ConsultarPorIdAsync(string id, CancellationToken ct) =>
        catalogo.Operadores.SingleOrDefaultAsync(operador => operador.Id == id, ct);

    public async Task<IReadOnlyList<Operador>> ConsultarTodosAsync(CancellationToken ct) =>
        await catalogo.Operadores.OrderBy(operador => operador.Id).ToListAsync(ct);

    public Task<Operador?> ConsultarPorNitAsync(string nit, CancellationToken ct) =>
        catalogo.Operadores.SingleOrDefaultAsync(operador => operador.Nit == nit, ct);
}
