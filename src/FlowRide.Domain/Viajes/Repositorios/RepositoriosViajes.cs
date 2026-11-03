using FlowRide.Domain.Comun.Repositorios;
using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Viajes.Repositorios;

/// <summary>
/// Lado de escritura de los viajes (ops.viaje), con sus paradas e incidentes. Las consultas de pantalla van por el lado de
/// lectura; crear y modificar los confirma la unidad de trabajo con su auditoría.
/// </summary>
public interface IViajeRepositorio : ICrearRepositorio<Viaje>
{
    /// <summary>El viaje con sus paradas e incidentes, rastreado para cambiarlo.</summary>
    Task<Viaje?> ConsultarPorIdAsync(Guid viajeId, CancellationToken ct);

    /// <summary>El viaje de la asignación (rastreado), o null si todavía no fue aceptada.</summary>
    Task<Viaje?> ConsultarPorAsignacionAsync(Guid asignacionId, CancellationToken ct);
}

/// <summary>Calificaciones de los viajes (ops.calificacion): solo se crean.</summary>
public interface ICalificacionRepositorio : ICrearRepositorio<Calificacion>
{
    Task<bool> ExisteAsync(Guid viajeId, Guid pasajeroId, CancellationToken ct);
}
