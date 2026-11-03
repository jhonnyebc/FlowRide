using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>Rutas del borde en el catálogo de plataforma (<c>plataforma.ruta_operador</c>).</summary>
public sealed class RutaOperadorRepositorioAdaptador(CatalogoDbContext catalogo) : IRutaOperadorRepositorio
{
    public async Task CrearAsync(RutaOperador entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        catalogo.Rutas.Add(entidad);
        await catalogo.SaveChangesAsync(ct);
    }

    public async Task ModificarAsync(string id, RutaOperador entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        if (entidad.OperadorId != id)
            throw new ArgumentException($"El Id '{id}' no coincide con el de la ruta '{entidad.OperadorId}'.", nameof(id));
        if (catalogo.Entry(entidad).State == EntityState.Detached)
            catalogo.Rutas.Update(entidad);
        await catalogo.SaveChangesAsync(ct);
    }

    public Task<RutaOperador?> ConsultarPorIdAsync(string id, CancellationToken ct) =>
        catalogo.Rutas.SingleOrDefaultAsync(ruta => ruta.OperadorId == id, ct);

    public async Task<IReadOnlyList<RutaOperador>> ConsultarTodosAsync(CancellationToken ct) =>
        await catalogo.Rutas.AsNoTracking().OrderBy(ruta => ruta.OperadorId).ToListAsync(ct);

    public Task<RutaOperador?> ConsultarPorSubdominioAsync(string subdominio, CancellationToken ct) =>
        catalogo.Rutas.AsNoTracking().SingleOrDefaultAsync(ruta => ruta.Subdominio == subdominio, ct);

    public Task<RutaOperador?> ConsultarPorNumeroAsync(string phoneNumberId, CancellationToken ct) =>
        catalogo.Rutas.AsNoTracking().SingleOrDefaultAsync(ruta => ruta.PhoneNumberId == phoneNumberId, ct);

    public async Task<IReadOnlyList<RutaOperador>> ConsultarDeOperadoresActivosAsync(CancellationToken ct) =>
        await (from ruta in catalogo.Rutas.AsNoTracking()
               join operador in catalogo.Operadores on ruta.OperadorId equals operador.Id
               where operador.Estado == EstadoOperador.Activo
               orderby ruta.OperadorId
               select ruta).ToListAsync(ct);
}
