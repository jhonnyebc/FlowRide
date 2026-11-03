namespace FlowRide.Domain.Transversal;

/// <summary>
/// Violación de una regla de negocio que el usuario puede corregir (dato inválido, recurso inexistente, estado que no
/// permite la operación). La API la convierte en una respuesta 400 con el mensaje; la CLI, en un error legible.
/// Los errores técnicos (base caída, bug) no usan esta excepción.
/// </summary>
public sealed class FlowRideExcepcion : Exception
{
    public FlowRideExcepcion(string mensaje) : base(mensaje)
    {
    }

    public FlowRideExcepcion(string mensaje, Exception causa) : base(mensaje, causa)
    {
    }

    public FlowRideExcepcion()
    {
    }
}
