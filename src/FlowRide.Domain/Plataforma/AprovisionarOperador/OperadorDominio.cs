using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Plataforma.AprovisionarOperador;

/// <summary>
/// Reglas de negocio sobre operadores que necesitan consultar el catálogo. Las que dependen solo del propio
/// operador viven en la entidad <see cref="Operador"/>.
/// </summary>
public sealed class OperadorDominio(IOperadorRepositorio operadores)
{
    public Task<Operador?> ConsultarAsync(string tenantId, CancellationToken ct) =>
        operadores.ConsultarPorIdAsync(tenantId, ct);

    public async Task<Operador> ValidarOperadorExistaAsync(string tenantId, CancellationToken ct) =>
        await operadores.ConsultarPorIdAsync(tenantId, ct)
        ?? throw new FlowRideExcepcion($"El operador '{tenantId}' no está registrado en el catálogo.");

    public static void ValidarIdentificador(string tenantId)
    {
        if (!Operador.EsIdValido(tenantId))
            throw new FlowRideExcepcion(
                $"El Id '{tenantId}' no es válido: usa minúsculas, números y guiones, con máximo 40 caracteres (por ejemplo 'tenant-a').");
    }

    /// <summary>El NIT identifica a un único operador en toda la plataforma.</summary>
    public async Task ValidarNitDisponibleAsync(string nit, string tenantId, CancellationToken ct)
    {
        var titular = await operadores.ConsultarPorNitAsync(nit.Trim(), ct);
        if (titular is not null && titular.Id != tenantId)
            throw new FlowRideExcepcion($"El NIT {nit} ya está registrado para el operador '{titular.Id}'.");
    }

    /// <summary>Un operador suspendido no se reaprovisiona: se reactiva de forma explícita.</summary>
    public static void ValidarNoSuspendido(Operador operador)
    {
        ArgumentNullException.ThrowIfNull(operador);
        if (operador.Estado == EstadoOperador.Suspendido)
            throw new FlowRideExcepcion($"El operador '{operador.Id}' está suspendido; reactívalo antes de aprovisionarlo.");
    }
}
