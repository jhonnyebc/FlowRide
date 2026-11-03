using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Operacion.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Application.Operacion.CasosUso;

/// <summary>
/// Alta de un conductor con sus datos operativos y su licencia (RF-26). Interactúa solo por WhatsApp, así que no
/// tiene cuenta en el Panel Web. El teléfono y la licencia se guardan cifrados (ADR-0014).
/// </summary>
public sealed class RegistrarConductorCasoUso(
    UsuarioDominio usuarioDominio,
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarConductorDominio, Conductor>
{
    public async Task<Conductor> EjecutarAsync(RegistrarConductorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, null, ct);
        if (datos.Correo is not null)
        {
            UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
            await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, null, ct);
        }
        await conductorDominio.ValidarLicenciaDisponibleAsync(datos.NumeroLicencia, null, ct);

        var ahora = reloj.GetUtcNow();
        var conductor = new Conductor(Guid.CreateVersion7(ahora), datos.Nombre, datos.TelefonoWhatsApp,
            RolesSistema.ConductorId, ahora, datos.NumeroLicencia, datos.CategoriaLicencia, datos.VencimientoLicencia,
            datos.Correo, aptoServicioCritico: datos.AptoServicioCritico);
        await conductores.CrearAsync(conductor, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoConductor,
            AccionesAuditoria.Crear, conductor.Id, ahora, despues: Auditar.Valores(("estado", conductor.Estado))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return conductor;
    }
}

/// <summary>Datos de contacto y licencia del conductor (RF-27).</summary>
public sealed class ActualizarConductorCasoUso(
    UsuarioDominio usuarioDominio,
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarConductorDominio, Conductor>
{
    public async Task<Conductor> EjecutarAsync(ActualizarConductorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, conductor.Id, ct);
        if (datos.Correo is not null)
        {
            UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
            await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, conductor.Id, ct);
        }
        await conductorDominio.ValidarLicenciaDisponibleAsync(datos.NumeroLicencia, conductor.Id, ct);

        conductor.ActualizarDatos(datos.Nombre, datos.TelefonoWhatsApp, datos.Correo);
        conductor.ActualizarLicencia(datos.NumeroLicencia, datos.CategoriaLicencia, datos.VencimientoLicencia);
        conductor.CambiarAptitudServicioCritico(datos.AptoServicioCritico);
        await conductores.ModificarAsync(conductor.Id, conductor, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoConductor,
            AccionesAuditoria.Actualizar, conductor.Id, reloj.GetUtcNow()), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return conductor;
    }
}

/// <summary>
/// Activación e inactivación operativa del conductor (RF-28): Activo, Suspendido o Inactivo. Es distinto del acceso
/// del usuario: un conductor suspendido sigue identificado, pero el motor no lo considera.
/// </summary>
public sealed class CambiarEstadoConductorCasoUso(
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarEstadoConductorDominio, Conductor>
{
    public async Task<Conductor> EjecutarAsync(CambiarEstadoConductorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);
        if (conductor.Estado == datos.Estado)
            return conductor;

        var antes = Auditar.Valores(("estado", conductor.Estado));
        conductor.CambiarEstado(datos.Estado);
        await conductores.ModificarAsync(conductor.Id, conductor, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoConductor,
            AccionesAuditoria.CambiarEstado, conductor.Id, reloj.GetUtcNow(), antes,
            Auditar.Valores(("estado", conductor.Estado))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return conductor;
    }
}

public sealed class BuscarConductoresCasoUso(IConductorRepositorio conductores)
    : ICasoUsoConRetorno<BuscarConductoresDominio, PaginaResultado<Conductor>>
{
    public Task<PaginaResultado<Conductor>> EjecutarAsync(BuscarConductoresDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return conductores.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}

/// <summary>Un conductor con sus turnos y documentos.</summary>
public sealed class ConsultarConductorCasoUso(ConductorDominio conductorDominio) : ICasoUsoConRetorno<Guid, Conductor>
{
    public Task<Conductor> EjecutarAsync(Guid datos, CancellationToken ct) => conductorDominio.ValidarExistaAsync(datos, ct);
}

/// <summary>
/// Programa un turno del conductor. Dos turnos vigentes del mismo conductor no se solapan: lo valida el agregado y,
/// ante escrituras concurrentes, la restricción EXCLUDE de la base (ADR-0007).
/// </summary>
public sealed class ProgramarTurnoCasoUso(
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ProgramarTurnoDominio, Turno>
{
    public async Task<Turno> EjecutarAsync(ProgramarTurnoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ConductorDominio.ValidarFranja(datos.Inicio, datos.Fin);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);

        var ahora = reloj.GetUtcNow();
        Turno turno;
        try
        {
            turno = conductor.ProgramarTurno(Guid.CreateVersion7(ahora), datos.Inicio, datos.Fin);
        }
        catch (InvalidOperationException error)
        {
            throw new FlowRideExcepcion(error.Message, error);
        }

        await conductores.ModificarAsync(conductor.Id, conductor, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoTurno,
            AccionesAuditoria.Programar, turno.Id, ahora,
            despues: Auditar.Valores(("conductor_id", conductor.Id), ("inicio", turno.Inicio), ("fin", turno.Fin), ("zona", turno.ZonaHoraria))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return turno;
    }
}

/// <summary>
/// Pone al día con el reloj el estado de los turnos: Programado → Activo al llegar su inicio y → Finalizado al llegar su
/// fin (decisión del usuario, 2026-10-27). Lo dispara el worker de la cola de asignación en cada vuelta. No se audita:
/// el cambio sale solo de la hora y siempre se puede reconstruir con la franja del turno.
/// </summary>
public sealed class ActualizarEstadoTurnosCasoUso(
    IConductorRepositorio conductores,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarEstadoTurnosDominio, int>
{
    public async Task<int> EjecutarAsync(ActualizarEstadoTurnosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var ahora = reloj.GetUtcNow();
        var cambiados = 0;
        foreach (var conductor in await conductores.ConsultarConTurnosPorActualizarAsync(ahora, datos.LoteMaximo, ct))
        {
            var cambios = conductor.ActualizarTurnosPorHora(ahora);
            if (cambios == 0)
                continue;
            await conductores.ModificarAsync(conductor.Id, conductor, ct);
            cambiados += cambios;
        }

        if (cambiados > 0)
            await unidadTrabajo.GuardarCambiosAsync(ct);
        return cambiados;
    }
}

/// <summary>Conductores disponibles para una franja según lo programado (RF-07).</summary>
public sealed class ConsultarDisponibilidadCasoUso(IConductorRepositorio conductores, IAsignacionRepositorio asignaciones)
    : ICasoUsoConRetorno<ConsultarDisponibilidadDominio, IReadOnlyList<Conductor>>
{
    public async Task<IReadOnlyList<Conductor>> EjecutarAsync(ConsultarDisponibilidadDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var franja = ConductorDominio.ValidarFranja(datos.Inicio, datos.Fin);

        var candidatos = await conductores.ConsultarConTurnoQueCubreAsync(franja, ct);
        var ocupados = await asignaciones.ConsultarConductoresOcupadosAsync(franja, ct);
        return ConductorDominio.FiltrarDisponibles(candidatos, ocupados, franja, datos.Inicio.Fecha);
    }
}

/// <summary>
/// Sube un soporte del conductor al bucket del operador, bajo <c>documents/</c> (ADR-0013). Si falla al guardar en la
/// base, borra el archivo para no dejar objetos huérfanos.
/// </summary>
public sealed class SubirDocumentoConductorCasoUso(
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IAlmacenObjetos almacen,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<SubirDocumentoConductorDominio, DocumentoConductor>
{
    public async Task<DocumentoConductor> EjecutarAsync(SubirDocumentoConductorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);

        var ahora = reloj.GetUtcNow();
        var documentoId = Guid.CreateVersion7(ahora);
        var ruta = $"documents/{conductor.Id}/{documentoId}{Path.GetExtension(datos.NombreArchivo).ToLowerInvariant()}";
        await almacen.GuardarAsync(ruta, datos.Contenido, datos.TipoContenido, ct);

        try
        {
            var documento = conductor.AgregarDocumento(documentoId, datos.Tipo, ruta, datos.FechaVencimiento);
            await conductores.ModificarAsync(conductor.Id, conductor, ct);
            await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoDocumentoConductor,
                AccionesAuditoria.Crear, documento.Id, ahora,
                despues: Auditar.Valores(("conductor_id", conductor.Id), ("tipo", documento.Tipo))), ct);
            await unidadTrabajo.GuardarCambiosAsync(ct);
            return documento;
        }
        catch
        {
            await almacen.EliminarAsync(ruta, CancellationToken.None);
            throw;
        }
    }
}

public sealed record UrlDocumento(Uri Url, DateTimeOffset ExpiraEn);

/// <summary>URL firmada de descarga del documento, válida 15 minutos (ADR-0013).</summary>
public sealed class ObtenerUrlDocumentoConductorCasoUso(
    ConductorDominio conductorDominio,
    IAlmacenObjetos almacen,
    TimeProvider reloj) : ICasoUsoConRetorno<DocumentoConductorRefDominio, UrlDocumento>
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(15);

    public async Task<UrlDocumento> EjecutarAsync(DocumentoConductorRefDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);
        var documento = conductor.Documentos.SingleOrDefault(d => d.Id == datos.DocumentoId)
                        ?? throw new FlowRideExcepcion($"El conductor no tiene el documento {datos.DocumentoId}.");

        var url = await almacen.UrlDescargaAsync(documento.ReferenciaBlob, Vigencia, ct);
        return new UrlDocumento(url, reloj.GetUtcNow().Add(Vigencia));
    }
}

/// <summary>Quita el documento y, después de confirmar, borra el archivo del bucket (sin retención inmutable).</summary>
public sealed class EliminarDocumentoConductorCasoUso(
    ConductorDominio conductorDominio,
    IConductorRepositorio conductores,
    IAlmacenObjetos almacen,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<DocumentoConductorRefDominio, DocumentoConductor>
{
    public async Task<DocumentoConductor> EjecutarAsync(DocumentoConductorRefDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var conductor = await conductorDominio.ValidarExistaAsync(datos.ConductorId, ct);
        if (conductor.Documentos.All(d => d.Id != datos.DocumentoId))
            throw new FlowRideExcepcion($"El conductor no tiene el documento {datos.DocumentoId}.");

        var documento = conductor.QuitarDocumento(datos.DocumentoId);
        await conductores.ModificarAsync(conductor.Id, conductor, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoDocumentoConductor,
            AccionesAuditoria.Eliminar, documento.Id, reloj.GetUtcNow(),
            Auditar.Valores(("conductor_id", conductor.Id), ("tipo", documento.Tipo))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);

        await almacen.EliminarAsync(documento.ReferenciaBlob, ct);
        return documento;
    }
}
