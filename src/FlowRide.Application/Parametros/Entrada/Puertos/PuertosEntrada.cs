using FlowRide.Application.Parametros.Entrada.Dto;

namespace FlowRide.Application.Parametros.Entrada.Puertos;

public interface IParametrosPuertoEntrada
{
    Task<IReadOnlyList<ParametroRespuesta>> ConsultarAsync(CancellationToken ct);

    Task<ParametroRespuesta> CambiarAsync(string clave, CambiarParametroPeticion peticion, Guid? actorId, CancellationToken ct);
}
