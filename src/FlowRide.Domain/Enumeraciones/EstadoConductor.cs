using System.Text.Json.Serialization;

namespace FlowRide.Domain.Enumeraciones;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoConductor
{
    Activo,
    Suspendido,
    Inactivo
}
