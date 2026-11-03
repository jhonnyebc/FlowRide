using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;

namespace FlowRide.Application.Asignaciones.Entrada.Puertos;

// actorId: usuario interno autenticado (auditoría). La asignación automática la ejecuta el sistema.

public interface IAsignacionesPuertoEntrada
{
    /// <summary>Asigna ya una solicitud pendiente con el motor (RF-10).</summary>
    Task<ResultadoAsignacionRespuesta> AsignarAsync(Guid solicitudId, CancellationToken ct);

    /// <summary>Solicitudes de la cola en el orden en que se atienden (para el worker).</summary>
    Task<IReadOnlyList<Guid>> PendientesAsync(int maximo, CancellationToken ct);

    Task<AsignacionRespuesta> AsignarManualAsync(Guid solicitudId, CandidatoManualPeticion peticion, Guid? actorId, CancellationToken ct);

    Task DevolverAColaAsync(Guid solicitudId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<AsignacionRespuesta> ReasignarAsync(Guid asignacionId, CandidatoManualPeticion peticion, Guid? actorId, CancellationToken ct);

    /// <summary>Devuelve las solicitudes que volvieron a la cola.</summary>
    Task<IReadOnlyList<Guid>> AbandonarTurnoAsync(Guid conductorId, Guid turnoId, MotivoPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<IReadOnlyList<DecisionLectura>> DecisionesAsync(Guid solicitudId, CancellationToken ct);

    Task<PaginaRespuesta<AsignacionLectura>> BuscarAsync(BusquedaAsignacionesPeticion peticion, CancellationToken ct);
}

public interface IConfiguracionMotorPuertoEntrada
{
    Task<IReadOnlyList<ConfiguracionMotorRespuesta>> ConsultarAsync(CancellationToken ct);

    Task<ConfiguracionMotorRespuesta> CrearAsync(ConfiguracionMotorPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<ConfiguracionMotorRespuesta> ActivarAsync(int version, Guid? actorId, CancellationToken ct);
}
