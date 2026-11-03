using System.Text.Json.Serialization;

namespace FlowRide.Domain.Auditoria;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ResultadoAuditoria
{
    Exitoso,
    Fallido
}
