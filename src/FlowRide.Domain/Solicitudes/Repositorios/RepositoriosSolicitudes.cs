using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Comun.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Solicitudes.Repositorios;

/// <summary>
/// Lado de escritura de las solicitudes (RF-09, RF-32). Las consultas de pantalla van por el lado de lectura (CQRS
/// ligero). Crear y modificar dejan el cambio pendiente: lo confirma la unidad de trabajo con su auditoría.
/// </summary>
public interface ISolicitudRepositorio
    : ICrearRepositorio<SolicitudViaje>, IConsultarRepositorio<Guid, SolicitudViaje>, IModificarRepositorio<Guid, SolicitudViaje>
{
    /// <summary>¿El pasajero tiene otra solicitud activa cuya franja se solapa con esta?</summary>
    Task<bool> ExisteActivaQueSeSolapaAsync(Guid pasajeroId, FranjaHoraria franja, Guid? excluirSolicitudId, CancellationToken ct);
}

public sealed record FiltroItinerarios(Guid? EmpresaClienteId = null, string? CodigoServicio = null, DateOnly? Desde = null, DateOnly? Hasta = null);

public interface IItinerarioRepositorio : ICrearRepositorio<Itinerario>, IConsultarRepositorio<Guid, Itinerario>
{
    /// <summary>Fechas en las que la empresa ya tiene ese código de servicio (único por empresa, código y fecha).</summary>
    Task<IReadOnlySet<DateOnly>> ConsultarFechasOcupadasAsync(
        Guid empresaClienteId, string codigoServicio, DateOnly desde, DateOnly hasta, CancellationToken ct);

    Task<PaginaResultado<Itinerario>> BuscarAsync(FiltroItinerarios filtro, Paginacion paginacion, CancellationToken ct);
}
