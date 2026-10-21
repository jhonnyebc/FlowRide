using System.Text.Json.Serialization;

namespace FlowRide.Domain.Enumeraciones;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TipoDocumento
{
    Licencia,
    Rtm,
    Soat,
    Otro
}
