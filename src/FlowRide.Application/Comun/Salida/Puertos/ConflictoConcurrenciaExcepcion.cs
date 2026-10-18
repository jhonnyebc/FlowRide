namespace FlowRide.Application.Comun.Salida.Puertos;

/// <summary>
/// Otro cambio confirmado antes impidió guardar este (ADR-0007): un token de versión que ya no coincide o una restricción
/// de exclusión (el conductor o el vehículo ya quedó ocupado en esa franja). Se reintenta o se informa al usuario (409).
/// </summary>
public sealed class ConflictoConcurrenciaExcepcion : Exception
{
    public ConflictoConcurrenciaExcepcion(string mensaje, Exception causa) : base(mensaje, causa)
    {
    }

    public ConflictoConcurrenciaExcepcion(string mensaje) : base(mensaje)
    {
    }

    public ConflictoConcurrenciaExcepcion()
    {
    }
}
