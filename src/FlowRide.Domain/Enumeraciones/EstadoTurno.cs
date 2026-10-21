using System.Text.Json.Serialization;

namespace FlowRide.Domain.Enumeraciones;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoTurno
{
    Programado,
    Activo,
    Finalizado,
    Abandonado
}
