using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Asignaciones.Repositorios;

/// <summary>Configuraciones del motor del Silo (motor.configuracion_asignacion), con sus reglas.</summary>
public interface IConfiguracionAsignacionRepositorio
    : ICrearRepositorio<ConfiguracionAsignacion>, IConsultarRepositorio<Guid, ConfiguracionAsignacion>,
        IModificarRepositorio<Guid, ConfiguracionAsignacion>
{
    Task<ConfiguracionAsignacion?> ConsultarActivaAsync(CancellationToken ct);

    Task<ConfiguracionAsignacion?> ConsultarPorVersionAsync(int version, CancellationToken ct);

    Task<int> UltimaVersionAsync(CancellationToken ct);
}

/// <summary>Decision log persistente: solo se crea.</summary>
public interface IDecisionAsignacionRepositorio : ICrearRepositorio<DecisionAsignacion>;
