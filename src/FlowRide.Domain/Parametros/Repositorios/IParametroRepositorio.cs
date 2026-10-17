using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Parametros.Repositorios;

/// <summary>Parámetros guardados en el Silo (<c>ops.parametro</c>). Los cambios los confirma la unidad de trabajo.</summary>
public interface IParametroRepositorio
    : ICrearRepositorio<Parametro>, IConsultarRepositorio<string, Parametro>, IModificarRepositorio<string, Parametro>;
