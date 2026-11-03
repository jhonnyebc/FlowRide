using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.GestionNotificaciones;
using FlowRide.Domain.Notificaciones.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Notificaciones.CasosUso;

public sealed class ConsultarCorreosPendientesCasoUso(ICorreoSalienteRepositorio correos, TimeProvider reloj)
    : ICasoUsoConRetorno<ConsultarCorreosPendientesDominio, IReadOnlyList<Guid>>
{
    public Task<IReadOnlyList<Guid>> EjecutarAsync(ConsultarCorreosPendientesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return correos.ConsultarPendientesAsync(reloj.GetUtcNow(), Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

/// <summary>Envía un correo de la bandeja (alertas críticas); un error se reintenta con espera creciente. Devuelve si se envió.</summary>
public sealed class EnviarCorreoCasoUso(
    ICorreoSalienteRepositorio correos,
    IEnviadorCorreo enviador,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<EnviarCorreoDominio, bool>
{
    public async Task<bool> EjecutarAsync(EnviarCorreoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (await correos.ConsultarPorIdAsync(datos.CorreoId, ct) is not { } correo || !correo.ListoParaEnviar(reloj.GetUtcNow()))
            return false;

        try
        {
            await enviador.EnviarAsync(correo.Destinatario, correo.Asunto, correo.Cuerpo, ct);
            correo.MarcarEnviado(reloj.GetUtcNow());
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            correo.RegistrarFallo($"{error.GetType().Name}: {error.Message}", reloj.GetUtcNow());
        }

        await unidadTrabajo.GuardarCambiosAsync(ct);
        return correo.Estado == EstadoCorreo.Enviado;
    }
}

/// <summary>Alertas del panel (RF-45), las más recientes primero.</summary>
public sealed class BuscarAlertasCasoUso(IConsultasAlertas consultas)
    : ICasoUsoConRetorno<BuscarAlertasDominio, PaginaResultado<AlertaLectura>>
{
    public Task<PaginaResultado<AlertaLectura>> EjecutarAsync(BuscarAlertasDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return consultas.BuscarAsync(datos.SoloNoLeidas, datos.Paginacion, ct);
    }
}

/// <summary>Alertas posteriores a una secuencia: lo que el envío en vivo (SSE) todavía no mandó.</summary>
public sealed class AlertasPosterioresCasoUso(IConsultasAlertas consultas)
    : ICasoUsoConRetorno<AlertasPosterioresDominio, IReadOnlyList<AlertaLectura>>
{
    public Task<IReadOnlyList<AlertaLectura>> EjecutarAsync(AlertasPosterioresDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return consultas.PosterioresAsync(Math.Max(0, datos.DespuesDe), Math.Clamp(datos.Maximo, 1, 200), ct);
    }
}

public sealed class MarcarAlertaLeidaCasoUso(IAlertaRepositorio alertas, IUnidadTrabajo unidadTrabajo, TimeProvider reloj)
    : ICasoUsoConRetorno<MarcarAlertaLeidaDominio, Alerta>
{
    public async Task<Alerta> EjecutarAsync(MarcarAlertaLeidaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var alerta = await alertas.ConsultarPorIdAsync(datos.AlertaId, ct)
                     ?? throw new FlowRideExcepcion($"La alerta {datos.AlertaId} no existe.");
        if (alerta.MarcarLeida(datos.ActorId, reloj.GetUtcNow()))
            await unidadTrabajo.GuardarCambiosAsync(ct);
        return alerta;
    }
}

public sealed class ConsultarPlantillasCasoUso(IPlantillaWhatsAppRepositorio plantillas)
    : ICasoUsoConRetorno<ConsultarPlantillasDominio, IReadOnlyList<PlantillaWhatsApp>>
{
    public Task<IReadOnlyList<PlantillaWhatsApp>> EjecutarAsync(ConsultarPlantillasDominio datos, CancellationToken ct) =>
        plantillas.ConsultarTodasAsync(ct);
}

/// <summary>
/// El Administrador registra la plantilla que Meta aprobó para una clave (nombre, idioma y estado; ADR-0016). Con auditoría.
/// </summary>
public sealed class ActualizarPlantillaCasoUso(
    IPlantillaWhatsAppRepositorio plantillas,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarPlantillaDominio, PlantillaWhatsApp>
{
    public async Task<PlantillaWhatsApp> EjecutarAsync(ActualizarPlantillaDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var plantilla = await plantillas.ConsultarAsync(datos.Clave, ct)
                        ?? throw new FlowRideExcepcion($"No hay plantilla registrada para el mensaje '{datos.Clave}'.");
        var antes = Auditar.Valores(("nombre", plantilla.Nombre), ("idioma", plantilla.Idioma), ("estado", plantilla.Estado));

        plantilla.Actualizar(datos.Nombre, datos.Idioma, datos.Estado, datos.ActorId, ahora);
        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoPlantilla, AccionesAuditoria.Actualizar), AccionesAuditoria.RecursoPlantilla,
            plantilla.Clave, ahora, ResultadoAuditoria.Exitoso, valoresAntes: antes,
            valoresDespues: Auditar.Valores(("nombre", plantilla.Nombre), ("idioma", plantilla.Idioma), ("estado", plantilla.Estado))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return plantilla;
    }
}
