namespace FlowRide.Domain.Entidades;

/// <summary>Rol de aplicación (RBAC, ADR-0003): Administrador del operador, Operador logístico, Pasajero o Conductor.</summary>
public sealed class Rol
{
    public Rol(Guid id, string nombre, string descripcion)
    {
        Id = Guardas.Id(id, "El rol requiere un Id.", nameof(id));
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        Descripcion = Guardas.Texto(descripcion, nameof(descripcion));
    }

    public Guid Id { get; }
    public string Nombre { get; }
    public string Descripcion { get; }
}
