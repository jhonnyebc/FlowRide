using System.Text.Json.Serialization;

namespace FlowRide.Domain.Enumeraciones;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoSolicitud
{
    Pendiente,
    Asignada,
    SinCandidatos,
    EnCurso,
    Completada,
    Cancelada
}
