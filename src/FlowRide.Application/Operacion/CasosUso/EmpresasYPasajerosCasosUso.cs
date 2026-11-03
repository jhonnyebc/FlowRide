using FlowRide.Application.Comun.Auditoria;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;

namespace FlowRide.Application.Operacion.CasosUso;

/// <summary>Alta de una empresa cliente atendida por el operador (RF-04). Su NIT es único en el Silo.</summary>
public sealed class RegistrarEmpresaClienteCasoUso(
    EmpresaClienteDominio empresaDominio,
    IEmpresaClienteRepositorio empresas,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarEmpresaClienteDominio, EmpresaCliente>
{
    public async Task<EmpresaCliente> EjecutarAsync(RegistrarEmpresaClienteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        UsuarioDominio.ValidarFormatoCorreo(datos.CorreoContacto);
        await empresaDominio.ValidarNitDisponibleAsync(datos.Nit, ct);

        var ahora = reloj.GetUtcNow();
        var empresa = new EmpresaCliente(Guid.CreateVersion7(ahora), datos.Nombre, datos.Nit, datos.CorreoContacto);
        await empresas.CrearAsync(empresa, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoEmpresaCliente,
            AccionesAuditoria.Crear, empresa.Id, ahora, despues: Auditar.Valores(("activa", empresa.Activa))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return empresa;
    }
}

public sealed class ActualizarEmpresaClienteCasoUso(
    EmpresaClienteDominio empresaDominio,
    IEmpresaClienteRepositorio empresas,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarEmpresaClienteDominio, EmpresaCliente>
{
    public async Task<EmpresaCliente> EjecutarAsync(ActualizarEmpresaClienteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        UsuarioDominio.ValidarFormatoCorreo(datos.CorreoContacto);
        var empresa = await empresaDominio.ValidarExistaAsync(datos.EmpresaId, ct);

        empresa.ActualizarDatos(datos.Nombre, datos.CorreoContacto);
        await empresas.ModificarAsync(empresa.Id, empresa, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoEmpresaCliente,
            AccionesAuditoria.Actualizar, empresa.Id, reloj.GetUtcNow()), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return empresa;
    }
}

/// <summary>Una empresa inactiva no recibe pasajeros nuevos; sus pasajeros existentes no cambian.</summary>
public sealed class CambiarEstadoEmpresaClienteCasoUso(
    EmpresaClienteDominio empresaDominio,
    IEmpresaClienteRepositorio empresas,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarEstadoEmpresaClienteDominio, EmpresaCliente>
{
    public async Task<EmpresaCliente> EjecutarAsync(CambiarEstadoEmpresaClienteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var empresa = await empresaDominio.ValidarExistaAsync(datos.EmpresaId, ct);
        if (empresa.Activa == datos.Activa)
            return empresa;

        if (datos.Activa) empresa.Activar();
        else empresa.Desactivar();
        await empresas.ModificarAsync(empresa.Id, empresa, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoEmpresaCliente,
            datos.Activa ? AccionesAuditoria.Activar : AccionesAuditoria.Desactivar, empresa.Id, reloj.GetUtcNow(),
            Auditar.Valores(("activa", !datos.Activa)), Auditar.Valores(("activa", datos.Activa))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return empresa;
    }
}

public sealed class BuscarEmpresasClienteCasoUso(IEmpresaClienteRepositorio empresas)
    : ICasoUsoConRetorno<BuscarEmpresasClienteDominio, PaginaResultado<EmpresaCliente>>
{
    public Task<PaginaResultado<EmpresaCliente>> EjecutarAsync(BuscarEmpresasClienteDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return empresas.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}

/// <summary>
/// Alta de un pasajero (RF-05): usuario con rol Pasajero de una empresa cliente activa. No tiene cuenta en el Panel
/// Web: interactúa por WhatsApp, identificado por su número (ADR-0003, ADR-0008).
/// </summary>
public sealed class RegistrarPasajeroCasoUso(
    UsuarioDominio usuarioDominio,
    EmpresaClienteDominio empresaDominio,
    PasajeroDominio pasajeroDominio,
    IPasajeroRepositorio pasajeros,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<RegistrarPasajeroDominio, Pasajero>
{
    public async Task<Pasajero> EjecutarAsync(RegistrarPasajeroDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await empresaDominio.ValidarActivaAsync(datos.EmpresaClienteId, ct);
        await pasajeroDominio.ValidarCodigoDisponibleAsync(datos.EmpresaClienteId, datos.CodigoInterno, ct);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, null, ct);
        if (datos.Correo is not null)
        {
            UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
            await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, null, ct);
        }

        var ahora = reloj.GetUtcNow();
        var pasajero = new Pasajero(Guid.CreateVersion7(ahora), datos.Nombre, datos.TelefonoWhatsApp,
            RolesSistema.PasajeroId, ahora, datos.CodigoInterno, datos.EmpresaClienteId, datos.Correo);
        await pasajeros.CrearAsync(pasajero, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoPasajero,
            AccionesAuditoria.Crear, pasajero.Id, ahora, despues: Auditar.Valores(("activo", pasajero.Activo))), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return pasajero;
    }
}

/// <summary>Datos de contacto del pasajero. Su estado se cambia con la gestión de usuarios (activar/desactivar).</summary>
public sealed class ActualizarPasajeroCasoUso(
    UsuarioDominio usuarioDominio,
    PasajeroDominio pasajeroDominio,
    IPasajeroRepositorio pasajeros,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<ActualizarPasajeroDominio, Pasajero>
{
    public async Task<Pasajero> EjecutarAsync(ActualizarPasajeroDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var pasajero = await pasajeroDominio.ValidarExistaAsync(datos.PasajeroId, ct);
        await usuarioDominio.ValidarTelefonoDisponibleAsync(datos.TelefonoWhatsApp, pasajero.Id, ct);
        if (datos.Correo is not null)
        {
            UsuarioDominio.ValidarFormatoCorreo(datos.Correo);
            await usuarioDominio.ValidarCorreoDisponibleAsync(datos.Correo, pasajero.Id, ct);
        }

        pasajero.ActualizarDatos(datos.Nombre, datos.TelefonoWhatsApp, datos.Correo);
        await pasajeros.ModificarAsync(pasajero.Id, pasajero, ct);
        await auditoria.CrearAsync(Auditar.Exito(datos.ActorId, AccionesAuditoria.RecursoPasajero,
            AccionesAuditoria.Actualizar, pasajero.Id, reloj.GetUtcNow()), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        return pasajero;
    }
}

public sealed class BuscarPasajerosCasoUso(IPasajeroRepositorio pasajeros)
    : ICasoUsoConRetorno<BuscarPasajerosDominio, PaginaResultado<Pasajero>>
{
    public Task<PaginaResultado<Pasajero>> EjecutarAsync(BuscarPasajerosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return pasajeros.BuscarAsync(datos.Filtro, datos.Paginacion, ct);
    }
}
