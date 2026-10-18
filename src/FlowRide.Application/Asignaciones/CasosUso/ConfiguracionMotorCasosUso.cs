using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Asignaciones;
using FlowRide.Domain.Asignaciones.GestionAsignaciones;
using FlowRide.Domain.Asignaciones.Repositorios;
using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Assignment.Rules;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Asignaciones.CasosUso;

/// <summary>Catálogo de reglas del motor que conoce el stack (ADR-0009): una regla nueva se agrega aquí por DI.</summary>
public sealed record CatalogoReglas(IReadOnlyList<IAssignmentRule> Reglas);

/// <summary>Todas las versiones de la configuración del motor, de la más reciente a la más antigua.</summary>
public sealed class ConsultarConfiguracionesCasoUso(IConfiguracionAsignacionRepositorio configuraciones)
    : ICasoUsoConRetorno<ConsultarConfiguracionesDominio, IReadOnlyList<ConfiguracionAsignacion>>
{
    public async Task<IReadOnlyList<ConfiguracionAsignacion>> EjecutarAsync(ConsultarConfiguracionesDominio datos, CancellationToken ct) =>
        (await configuraciones.ConsultarTodosAsync(ct)).OrderByDescending(c => c.Version).ToList();
}

/// <summary>
/// Crea una versión nueva de la configuración del motor (RF-21) como borrador. Se valida antes de guardarla: reglas,
/// pesos, precedencias, obligatorias, dependencias y ciclos (ADR-0009). Activarla es un paso aparte.
/// </summary>
public sealed class CrearConfiguracionCasoUso(
    IConfiguracionAsignacionRepositorio configuraciones,
    CatalogoReglas catalogo,
    OperadorDelStack operador,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CrearConfiguracionDominio, ConfiguracionAsignacion>
{
    public async Task<ConfiguracionAsignacion> EjecutarAsync(CrearConfiguracionDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var version = await configuraciones.UltimaVersionAsync(ct) + 1;
        var configuracion = new ConfiguracionAsignacion(Guid.CreateVersion7(ahora), version, datos.Reglas, catalogo.Reglas, datos.ActorId, ahora);
        ValidacionConfiguracion.Exigir(configuracion, catalogo, operador);

        await configuraciones.CrearAsync(configuracion, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoConfiguracionMotor, AccionesAuditoria.Crear,
            configuracion.Id, ahora, despues: Auditar.Valores(("version", version), ("reglas", configuracion.Reglas.Count))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return configuracion;
    }
}

/// <summary>
/// Activa una versión de la configuración (RF-21): la activa pasa a Archivada y la elegida a Activa. Activar una versión
/// anterior es el rollback. Las decisiones siguientes registran la versión con la que se tomaron.
/// </summary>
public sealed class ActivarConfiguracionCasoUso(
    IConfiguracionAsignacionRepositorio configuraciones,
    CatalogoReglas catalogo,
    OperadorDelStack operador,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActivarConfiguracionDominio, ConfiguracionAsignacion>
{
    public async Task<ConfiguracionAsignacion> EjecutarAsync(ActivarConfiguracionDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var elegida = await configuraciones.ConsultarPorVersionAsync(datos.Version, ct)
                      ?? throw new FlowRideExcepcion($"La configuración v{datos.Version} no existe.");
        if (elegida.Estado == EstadoConfiguracion.Activa)
            return elegida;
        ValidacionConfiguracion.Exigir(elegida, catalogo, operador);

        var activa = await configuraciones.ConsultarActivaAsync(ct);
        activa?.Archivar();
        elegida.Activar(ahora);
        if (activa is not null) await configuraciones.ModificarAsync(activa.Id, activa, ct);
        await configuraciones.ModificarAsync(elegida.Id, elegida, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoConfiguracionMotor, AccionesAuditoria.Activar,
            elegida.Id, ahora, Auditar.Valores(("version_activa", activa?.Version)), Auditar.Valores(("version_activa", elegida.Version))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return elegida;
    }
}

internal static class ValidacionConfiguracion
{
    public static void Exigir(ConfiguracionAsignacion configuracion, CatalogoReglas catalogo, OperadorDelStack operador)
    {
        var resultado = new ConfigurationValidator().Validate(configuracion.ComoConfiguracionDelMotor(operador.TenantId), catalogo.Reglas);
        if (!resultado.IsValid)
            throw new FlowRideExcepcion("La configuración no es válida: " + string.Join(" ", resultado.Errors));
    }
}

/// <summary>Decisiones del motor sobre una solicitud, con cada candidato y el resultado de cada regla (ADR-0009).</summary>
public sealed class ConsultarDecisionesCasoUso(IConsultasAsignaciones consultas)
    : ICasoUsoConRetorno<ConsultarDecisionesDominio, IReadOnlyList<DecisionLectura>>
{
    public Task<IReadOnlyList<DecisionLectura>> EjecutarAsync(ConsultarDecisionesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return consultas.DecisionesAsync(datos.SolicitudId, ct);
    }
}

/// <summary>Asignaciones por conductor, solicitud, estado o fechas (RF-38).</summary>
public sealed class BuscarAsignacionesCasoUso(IConsultasAsignaciones consultas)
    : ICasoUsoConRetorno<BuscarAsignacionesDominio, PaginaResultado<AsignacionLectura>>
{
    public Task<PaginaResultado<AsignacionLectura>> EjecutarAsync(BuscarAsignacionesDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return consultas.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}
