namespace FlowRide.Domain.Entidades;

/// <summary>
/// Cliente empresarial atendido por el operador (aerolínea, minera, hospital). Su Id es el <c>empresaId</c>
/// del modelo: identifica al cliente dentro de la base del operador, nunca separa tenants (ADR-0002).
/// </summary>
public sealed class EmpresaCliente
{
    public EmpresaCliente(Guid id, string nombre, string nit, string correoContacto, bool activa = true)
    {
        Id = Guardas.Id(id, "La empresa cliente requiere un Id.", nameof(id));
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        Nit = Guardas.Texto(nit, nameof(nit));
        CorreoContacto = Guardas.Texto(correoContacto, nameof(correoContacto));
        Activa = activa;
    }

    public Guid Id { get; }
    public string Nombre { get; private set; }
    public string Nit { get; }
    public string CorreoContacto { get; private set; }
    public bool Activa { get; private set; }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    public void ActualizarDatos(string nombre, string correoContacto)
    {
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        CorreoContacto = Guardas.Texto(correoContacto, nameof(correoContacto));
    }
}
