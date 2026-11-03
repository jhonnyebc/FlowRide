using System.Text.Json;
using FlowRide.Application.Notificaciones.Catalogo;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.Repositorios;

namespace FlowRide.Application.Notificaciones.CasosUso;

/// <summary>
/// Notification Gateway (ADR-0015): decide canal y formato. Pasajero y conductor → WhatsApp, con texto de sesión dentro
/// de la ventana de 24 h y, fuera de ella, la plantilla aprobada de la clave (ADR-0016); si no la hay, es un error
/// permanente que se alerta al panel. Alertas → panel, y las críticas también por correo a los administradores.
/// Todo queda en bandejas (WhatsApp, alertas, correo) dentro de la misma transacción que marca el evento procesado.
/// </summary>
public sealed class GatewayNotificaciones(
    CatalogoMensajes catalogo,
    IUsuarioRepositorio usuarios,
    IMensajeEntranteRepositorio entrantes,
    IMensajeSalienteRepositorio salientes,
    IPlantillaWhatsAppRepositorio plantillas,
    IAlertaRepositorio alertas,
    ICorreoSalienteRepositorio correos,
    TimeProvider reloj)
{
    public const string ClaveFalloWhatsApp = "alerta.whatsapp.fallido";

    /// <summary>Notifica a un pasajero o conductor por WhatsApp. Devuelve false si no hay a quién (inexistente o inactivo).</summary>
    public async Task<bool> NotificarAsync(Guid usuarioId, string clave, IReadOnlyDictionary<string, string> valores, Guid eventId,
        CancellationToken ct)
    {
        var usuario = await usuarios.ConsultarPorIdAsync(usuarioId, ct);
        if (usuario is not { Activo: true } || usuario is not (Pasajero or Conductor))
            return false;

        var ahora = reloj.GetUtcNow();
        var mensaje = catalogo.Armar(clave, valores);
        var saliente = new MensajeSaliente(Guid.CreateVersion7(ahora), usuario.TelefonoWhatsApp, mensaje.Texto, ahora)
            .ComoNotificacion(clave, eventId);

        if (!VentanaConversacion.Abierta(await entrantes.UltimoRecibidoAsync(usuario.TelefonoWhatsApp, ct), ahora))
        {
            if (await plantillas.ConsultarAsync(clave, ct) is not { Utilizable: true } plantilla)
            {
                await AlertarAsync(ClaveFalloWhatsApp, SeveridadAlerta.Critica, new Dictionary<string, string>
                {
                    ["destinatario"] = usuario.Nombre,
                    ["clave"] = clave,
                    ["error"] = "pasaron más de 24 h desde su último mensaje y la plantilla de WhatsApp no está aprobada"
                }, usuario.Id, eventId, ct);
                return false;
            }
            saliente.ConPlantilla(plantilla.Nombre, plantilla.Idioma, JsonSerializer.Serialize(mensaje.Parametros));
        }

        await salientes.CrearAsync(saliente, ct);
        return true;
    }

    /// <summary>Alerta del panel (RF-45); si es crítica, también correo a cada administrador activo con correo.</summary>
    public async Task<Alerta> AlertarAsync(string clave, SeveridadAlerta severidad, IReadOnlyDictionary<string, string> valores,
        Guid? referenciaId, Guid? eventId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var (titulo, detalle) = catalogo.Armar(clave, valores).TituloYDetalle();
        var alerta = new Alerta(Guid.CreateVersion7(ahora), clave, severidad, titulo, detalle, ahora, referenciaId, eventId);
        await alertas.CrearAsync(alerta, ct);

        if (severidad == SeveridadAlerta.Critica)
        {
            foreach (var administrador in await usuarios.ConsultarActivosPorRolAsync(RolesSistema.AdministradorId, ct))
            {
                if (string.IsNullOrWhiteSpace(administrador.Correo))
                    continue;
                await correos.CrearAsync(new CorreoSaliente(Guid.CreateVersion7(ahora), administrador.Correo, $"[FlowRide] {titulo}",
                    $"{detalle}\n\nRevisa las alertas del Panel Web de FlowRide.", ahora, alerta.Id), ct);
            }
        }
        return alerta;
    }
}
