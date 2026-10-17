namespace FlowRide.Domain.Comun.Repositorios;

// Puertos de salida genéricos: cada repositorio de un agregado se arma con los que necesita.
// Viven en Domain (solo interfaces) para que los servicios de dominio puedan consultar sin depender de Application.

public interface ICrearRepositorio<in TEntidad>
{
    Task CrearAsync(TEntidad entidad, CancellationToken ct);
}

public interface IConsultarRepositorio<in TId, TEntidad>
{
    Task<TEntidad?> ConsultarPorIdAsync(TId id, CancellationToken ct);

    Task<IReadOnlyList<TEntidad>> ConsultarTodosAsync(CancellationToken ct);
}

public interface IModificarRepositorio<in TId, in TEntidad>
{
    Task ModificarAsync(TId id, TEntidad entidad, CancellationToken ct);
}

public interface IEliminarRepositorio<in TId>
{
    Task EliminarAsync(TId id, CancellationToken ct);
}
