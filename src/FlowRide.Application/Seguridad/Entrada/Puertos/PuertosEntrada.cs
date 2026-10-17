namespace FlowRide.Application.Seguridad.Entrada.Puertos;

public interface IAsegurarCifradoPuertoEntrada
{
    /// <summary>Devuelve cuántos valores se cifraron o recifraron.</summary>
    Task<int> AsegurarAsync(CancellationToken ct);
}
