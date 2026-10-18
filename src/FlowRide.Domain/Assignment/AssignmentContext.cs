using FlowRide.Domain.Assignment.Configuration;
using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Assignment;

/// <summary>Entrada completa de una decisión: con el mismo contexto el motor produce siempre el mismo resultado.</summary>
public sealed class AssignmentContext
{
    public AssignmentContext(
        SolicitudViaje solicitud,
        IEnumerable<Candidato> candidatos,
        AssignmentConfiguration configuration,
        DateTimeOffset? evaluadaEn = null)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(candidatos);
        ArgumentNullException.ThrowIfNull(configuration);

        Solicitud = solicitud;
        Candidatos = candidatos.ToArray();
        Configuration = configuration;
        EvaluadaEn = evaluadaEn ?? solicitud.RegistradaEn;
    }

    public SolicitudViaje Solicitud { get; }
    public IReadOnlyList<Candidato> Candidatos { get; }
    public AssignmentConfiguration Configuration { get; }

    /// <summary>Momento de la decisión; lo da el caso de uso (reloj inyectado), así el motor sigue siendo puro.</summary>
    public DateTimeOffset EvaluadaEn { get; }
}
