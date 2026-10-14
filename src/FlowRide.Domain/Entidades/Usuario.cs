namespace FlowRide.Domain.Entidades;

/// <summary>
/// Usuario interno de FlowRide. La autenticación la hace el proveedor de identidad (ADR-0003);
/// aquí se conservan el rol y el estado que FlowRide valida en cada operación.
/// </summary>
public class Usuario
{
    public Usuario(
        Guid id,
        string nombre,
        string telefonoWhatsApp,
        Guid rolId,
        DateTimeOffset creadoEn,
        string? correo = null,
        string? identidadExternaId = null)
    {
        Id = Guardas.Id(id, "El usuario requiere un Id.", nameof(id));
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        TelefonoWhatsApp = Guardas.Texto(telefonoWhatsApp, nameof(telefonoWhatsApp));
        RolId = Guardas.Id(rolId, "El usuario requiere un rol.", nameof(rolId));
        CreadoEn = creadoEn;
        Correo = Guardas.TextoOpcional(correo);
        IdentidadExternaId = Guardas.TextoOpcional(identidadExternaId);
        Activo = true;
    }

    public Guid Id { get; }
    public string Nombre { get; private set; }
    public string? Correo { get; private set; }
    public string TelefonoWhatsApp { get; private set; }

    /// <summary>Identificador del usuario en el proveedor OIDC (claim <c>sub</c>); solo para usuarios del Panel Web.</summary>
    public string? IdentidadExternaId { get; private set; }

    public Guid RolId { get; private set; }
    public bool Activo { get; private set; }
    public DateTimeOffset CreadoEn { get; }

    public void Activar() => Activo = true;

    /// <summary>La desactivación prevalece sobre la vigencia del token externo (ADR-0003).</summary>
    public void Desactivar() => Activo = false;

    public void CambiarRol(Guid rolId) => RolId = Guardas.Id(rolId, "El usuario requiere un rol.", nameof(rolId));

    public void ActualizarDatos(string nombre, string telefonoWhatsApp, string? correo)
    {
        Nombre = Guardas.Texto(nombre, nameof(nombre));
        TelefonoWhatsApp = Guardas.Texto(telefonoWhatsApp, nameof(telefonoWhatsApp));
        Correo = Guardas.TextoOpcional(correo);
    }

    public void VincularIdentidadExterna(string identidadExternaId) =>
        IdentidadExternaId = Guardas.Texto(identidadExternaId, nameof(identidadExternaId));
}
