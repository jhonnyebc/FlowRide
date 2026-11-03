namespace FlowRide.Application.Seguridad.Salida.Puertos;

/// <summary>
/// Deja cifrados con la clave vigente del operador los campos sensibles del Silo (ADR-0014): cifra los que estaban en
/// claro y recifra los de versiones anteriores de la clave. Es idempotente.
/// </summary>
public interface IRecifradorCampos
{
    /// <summary>Devuelve cuántos valores reescribió.</summary>
    Task<int> RecifrarAsync(CancellationToken ct);
}
