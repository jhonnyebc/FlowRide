using FlowRide.Application.Operacion.Entrada.Dto;

namespace FlowRide.Application.Operacion.Entrada.Puertos;

// Un puerto de entrada por recurso, con sus operaciones. actorId: usuario interno autenticado (auditoría).

public interface IEmpresasClientePuertoEntrada
{
    Task<EmpresaClienteRespuesta> RegistrarAsync(EmpresaClientePeticion peticion, Guid? actorId, CancellationToken ct);

    Task<EmpresaClienteRespuesta> ActualizarAsync(Guid empresaId, EmpresaClientePeticion peticion, Guid? actorId, CancellationToken ct);

    Task<EmpresaClienteRespuesta> CambiarEstadoAsync(Guid empresaId, bool activa, Guid? actorId, CancellationToken ct);

    Task<PaginaRespuesta<EmpresaClienteRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct);
}

public interface IPasajerosPuertoEntrada
{
    Task<PasajeroRespuesta> RegistrarAsync(PasajeroPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<PasajeroRespuesta> ActualizarAsync(Guid pasajeroId, PasajeroPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<PaginaRespuesta<PasajeroRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct);
}

public interface IConductoresPuertoEntrada
{
    Task<ConductorRespuesta> RegistrarAsync(ConductorPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<ConductorRespuesta> ActualizarAsync(Guid conductorId, ConductorPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<ConductorRespuesta> CambiarEstadoAsync(Guid conductorId, EstadoConductorPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<ConductorDetalleRespuesta> ConsultarAsync(Guid conductorId, CancellationToken ct);

    Task<PaginaRespuesta<ConductorRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct);

    Task<TurnoRespuesta> ProgramarTurnoAsync(Guid conductorId, TurnoPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<IReadOnlyList<ConductorDisponibleRespuesta>> ConsultarDisponiblesAsync(DateTime? inicio, DateTime? fin, string? zonaHoraria, CancellationToken ct);
}

public interface IDocumentosConductorPuertoEntrada
{
    Task<DocumentoConductorRespuesta> SubirAsync(
        Guid conductorId, DocumentoPeticion peticion, ArchivoPeticion? archivo, Guid? actorId, CancellationToken ct);

    Task<UrlDocumentoRespuesta> ObtenerUrlAsync(Guid conductorId, Guid documentoId, CancellationToken ct);

    Task EliminarAsync(Guid conductorId, Guid documentoId, Guid? actorId, CancellationToken ct);
}

public interface IVehiculosPuertoEntrada
{
    Task<VehiculoRespuesta> RegistrarAsync(VehiculoPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<VehiculoRespuesta> ActualizarAsync(Guid vehiculoId, VehiculoPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<VehiculoRespuesta> CambiarEstadoAsync(Guid vehiculoId, bool activo, Guid? actorId, CancellationToken ct);

    Task<VehiculoRespuesta> AsignarConductorHabitualAsync(
        Guid vehiculoId, ConductorHabitualPeticion peticion, Guid? actorId, CancellationToken ct);

    Task<PaginaRespuesta<VehiculoRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct);
}
