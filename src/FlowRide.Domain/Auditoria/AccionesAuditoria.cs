namespace FlowRide.Domain.Auditoria;

/// <summary>Acciones sensibles que quedan en la auditoría legal (ADR-0011), con el formato <c>recurso.accion</c>.</summary>
public static class AccionesAuditoria
{
    public const string RecursoUsuario = "usuario";

    public const string UsuarioCrear = "usuario.crear";
    public const string UsuarioActivar = "usuario.activar";
    public const string UsuarioDesactivar = "usuario.desactivar";
    public const string UsuarioCambiarRol = "usuario.cambiar_rol";

    public const string RecursoEmpresaCliente = "empresa_cliente";
    public const string RecursoPasajero = "pasajero";
    public const string RecursoConductor = "conductor";
    public const string RecursoTurno = "turno";
    public const string RecursoDocumentoConductor = "documento_conductor";
    public const string RecursoVehiculo = "vehiculo";
    public const string RecursoParametro = "parametro";
    public const string RecursoSolicitud = "solicitud";
    public const string RecursoItinerario = "itinerario";

    public const string Crear = "crear";
    public const string Actualizar = "actualizar";
    public const string Activar = "activar";
    public const string Desactivar = "desactivar";
    public const string CambiarEstado = "cambiar_estado";
    public const string Programar = "programar";
    public const string Eliminar = "eliminar";
    public const string AsignarConductorHabitual = "asignar_conductor_habitual";
    public const string Modificar = "modificar";
    public const string Cancelar = "cancelar";

    /// <summary>Acción con el formato <c>recurso.accion</c>.</summary>
    public static string De(string recurso, string accion) => $"{recurso}.{accion}";
}
