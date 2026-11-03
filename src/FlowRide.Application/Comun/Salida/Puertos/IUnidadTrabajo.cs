namespace FlowRide.Application.Comun.Salida.Puertos;

/// <summary>
/// Confirma en una sola transacción del Silo los cambios pendientes de los repositorios y los eventos de auditoría
/// que los registran (ADR-0007, ADR-0011): o se guardan todos o ninguno.
/// </summary>
public interface IUnidadTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct);

    /// <summary>Descarta los cambios pendientes sin guardarlos (p. ej. tras una regla de negocio que falló a mitad de un flujo).</summary>
    void DescartarCambios();
}
