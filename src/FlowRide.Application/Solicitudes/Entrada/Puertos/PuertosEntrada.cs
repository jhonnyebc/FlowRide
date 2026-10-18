using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;

namespace FlowRide.Application.Solicitudes.Entrada.Puertos;

// actorId: usuario interno autenticado. rol: su rol del Panel Web (RolesPanel), que decide qué puede ver de las
// solicitudes personales y si puede cancelar fuera de la ventana.

public interface ISolicitudesPuertoEntrada
{
    Task<SolicitudDetalleRespuesta> RegistrarAsync(SolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct);

    Task<SolicitudDetalleRespuesta> ModificarAsync(Guid solicitudId, SolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct);

    Task<SolicitudDetalleRespuesta> CancelarAsync(
        Guid solicitudId, CancelarSolicitudPeticion peticion, Guid? actorId, string rol, CancellationToken ct);

    Task<SolicitudDetalleRespuesta> ConsultarAsync(Guid solicitudId, string rol, CancellationToken ct);

    Task<PaginaRespuesta<SolicitudRespuesta>> BuscarAsync(BusquedaSolicitudesPeticion peticion, string rol, CancellationToken ct);
}

public interface IItinerariosPuertoEntrada
{
    Task<IReadOnlyList<ItinerarioRespuesta>> RegistrarAsync(ItinerariosPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<PaginaRespuesta<ItinerarioRespuesta>> BuscarAsync(BusquedaItinerariosPeticion peticion, CancellationToken ct);
}
