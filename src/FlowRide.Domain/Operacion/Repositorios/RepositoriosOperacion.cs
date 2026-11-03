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

    /// <summary>
    /// Conductores con algún turno atrasado respecto del reloj: programado que ya debió empezar o vigente que ya debió
    /// terminar. Incluye sus turnos para modificarlos; como mucho <paramref name="lote"/>.
    /// </summary>
    Task<IReadOnlyList<Conductor>> ConsultarConTurnosPorActualizarAsync(DateTimeOffset ahora, int lote, CancellationToken ct);
}

public interface IVehiculoRepositorio
    : ICrearRepositorio<Vehiculo>, IConsultarRepositorio<Guid, Vehiculo>, IModificarRepositorio<Guid, Vehiculo>
{
    Task<Vehiculo?> ConsultarPorPlacaAsync(string placa, CancellationToken ct);

    Task<Vehiculo?> ConsultarPorConductorHabitualAsync(Guid conductorId, CancellationToken ct);

    Task<PaginaResultado<Vehiculo>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct);
}

/// <summary>Asignaciones vistas desde la operación (Fases 5 y 6).</summary>
public interface IAsignacionRepositorio
{
    /// <summary>Conductores con una asignación vigente (asignada, aceptada o en curso) que se solapa con la franja.</summary>
    Task<IReadOnlySet<Guid>> ConsultarConductoresOcupadosAsync(FranjaHoraria franja, CancellationToken ct);

    /// <summary>Asignación vigente de la solicitud (rastreada, para cambiarla), o null si no tiene.</summary>
    Task<Asignacion?> ConsultarVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct);

    Task CrearAsync(Asignacion asignacion, CancellationToken ct);

    /// <summary>La asignación, rastreada para cambiarla.</summary>
    Task<Asignacion?> ConsultarPorIdAsync(Guid asignacionId, CancellationToken ct);

    /// <summary>Vehículos con una asignación vigente que se solapa con la franja.</summary>
    Task<IReadOnlySet<Guid>> ConsultarVehiculosOcupadosAsync(FranjaHoraria franja, CancellationToken ct);

    /// <summary>Asignaciones (no rechazadas, canceladas ni reemplazadas) de cada conductor desde la fecha (balance de carga).</summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarRecientesAsync(IReadOnlyCollection<Guid> conductorIds, DateTimeOffset desde, CancellationToken ct);

    /// <summary>Asignaciones vigentes del conductor que se solapan con la franja, rastreadas (abandono de turno).</summary>
    Task<IReadOnlyList<Asignacion>> ConsultarVigentesDelConductorAsync(Guid conductorId, FranjaHoraria franja, CancellationToken ct);

    /// <summary>Conductores que rechazaron la solicitud (o la dejaron vencer): el motor no se la vuelve a proponer.</summary>
    Task<IReadOnlySet<Guid>> ConsultarConductoresQueRechazaronAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Asignaciones que siguen esperando respuesta del conductor y se crearon antes de la fecha, las más viejas primero.</summary>
    Task<IReadOnlyList<Guid>> ConsultarSinRespuestaAsync(DateTimeOffset asignadasAntesDe, int maximo, CancellationToken ct);
}
