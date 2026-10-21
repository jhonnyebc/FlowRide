using System.Text.Json.Serialization;

namespace FlowRide.Domain.Plataforma;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoOperador
{
    /// <summary>Registrado en el catálogo; su Silo aún no termina de aprovisionarse.</summary>
    Aprovisionando,
    Activo,
    Suspendido
}
