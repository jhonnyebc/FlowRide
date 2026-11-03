using System.Text.Json.Serialization;

namespace FlowRide.Domain.Assignment.Results;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AssignmentOutcome
{
    Assigned,
    NoCandidates,
    InvalidConfiguration
}
