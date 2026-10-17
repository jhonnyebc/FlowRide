using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Plataforma.Repositorios;

/// <summary>Operadores del catálogo de plataforma. Un operador no se elimina: se suspende.</summary>
public interface IOperadorRepositorio :
    ICrearRepositorio<Operador>,
    IConsultarRepositorio<string, Operador>,
    IModificarRepositorio<string, Operador>
{
    Task<Operador?> ConsultarPorNitAsync(string nit, CancellationToken ct);
}
