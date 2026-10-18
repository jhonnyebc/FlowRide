using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Comun.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Operacion.Repositorios;

// Recursos operativos del Silo (RF-04, RF-26 a RF-28, RF-38, RF-41). Como en Identidad, crear y modificar dejan el
// cambio pendiente: lo confirma la unidad de trabajo junto con su evento de auditoría.

/// <summary>Filtro de los listados: texto libre (nombre, o valor exacto de un campo cifrado) y estado.</summary>
public sealed record FiltroRecursos(string? Texto = null, bool? Activo = null);

public sealed record FiltroConductores(string? Texto = null, EstadoConductor? Estado = null);

public sealed record FiltroPasajeros(string? Texto = null, Guid? EmpresaClienteId = null, bool? Activo = null);

public interface IEmpresaClienteRepositorio
    : ICrearRepositorio<EmpresaCliente>, IConsultarRepositorio<Guid, EmpresaCliente>, IModificarRepositorio<Guid, EmpresaCliente>
{
    Task<EmpresaCliente?> ConsultarPorNitAsync(string nit, CancellationToken ct);

    Task<PaginaResultado<EmpresaCliente>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct);
}

public interface IPasajeroRepositorio
    : ICrearRepositorio<Pasajero>, IConsultarRepositorio<Guid, Pasajero>, IModificarRepositorio<Guid, Pasajero>
{
    Task<Pasajero?> ConsultarPorCodigoAsync(Guid empresaClienteId, string codigoInterno, CancellationToken ct);

    Task<PaginaResultado<Pasajero>> BuscarAsync(FiltroPasajeros filtro, Paginacion paginacion, CancellationToken ct);
}

public interface IConductorRepositorio
    : ICrearRepositorio<Conductor>, IConsultarRepositorio<Guid, Conductor>, IModificarRepositorio<Guid, Conductor>
{
    Task<Conductor?> ConsultarPorLicenciaAsync(string numeroLicencia, CancellationToken ct);

    Task<PaginaResultado<Conductor>> BuscarAsync(FiltroConductores filtro, Paginacion paginacion, CancellationToken ct);

    /// <summary>
    /// Conductores que pueden cubrir la franja según lo programado (RF-07): acceso y estado operativo activos y un
    /// turno vigente que la contiene. Incluye sus turnos; la licencia y las asignaciones las valida el dominio.
    /// </summary>
    Task<IReadOnlyList<Conductor>> ConsultarConTurnoQueCubreAsync(FranjaHoraria franja, CancellationToken ct);
}

public interface IVehiculoRepositorio
    : ICrearRepositorio<Vehiculo>, IConsultarRepositorio<Guid, Vehiculo>, IModificarRepositorio<Guid, Vehiculo>
{
    Task<Vehiculo?> ConsultarPorPlacaAsync(string placa, CancellationToken ct);

    Task<Vehiculo?> ConsultarPorConductorHabitualAsync(Guid conductorId, CancellationToken ct);

    Task<PaginaResultado<Vehiculo>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct);
}

/// <summary>Asignaciones vistas desde la operación. La Fase 5 (motor de punta a punta) agrega lo que necesite.</summary>
public interface IAsignacionRepositorio
{
    /// <summary>Conductores con una asignación vigente (asignada, aceptada o en curso) que se solapa con la franja.</summary>
    Task<IReadOnlySet<Guid>> ConsultarConductoresOcupadosAsync(FranjaHoraria franja, CancellationToken ct);

    /// <summary>Asignación vigente de la solicitud (rastreada, para cambiarla), o null si no tiene.</summary>
    Task<Asignacion?> ConsultarVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct);
}
