using System.Text.Json.Serialization;

namespace FlowRide.Domain.Enumeraciones;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoAsignacion
{
    Asignada,
    Aceptada,
    Rechazada,
    EnCurso,
    Finalizada,
    Cancelada,
    Reemplazada
}
