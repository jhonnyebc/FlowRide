namespace FlowRide.Domain.Entidades;

/// <summary>Calificación del pasajero al conductor de un viaje finalizado (RF-54, PAS-AUD-03).</summary>
public sealed class Calificacion
{
    public const int PuntajeMinimo = 1;
    public const int PuntajeMaximo = 5;

    public Calificacion(Guid id, Guid viajeId, Guid pasajeroId, int puntaje, string? comentario, DateTimeOffset fechaHora)
    {
        Id = Guardas.Id(id, "La calificación requiere un Id.", nameof(id));
        ViajeId = Guardas.Id(viajeId, "La calificación requiere un viaje.", nameof(viajeId));
        PasajeroId = Guardas.Id(pasajeroId, "La calificación requiere un pasajero.", nameof(pasajeroId));
        if (puntaje is < PuntajeMinimo or > PuntajeMaximo)
            throw new ArgumentOutOfRangeException(nameof(puntaje), puntaje,
                $"El puntaje debe estar entre {PuntajeMinimo} y {PuntajeMaximo}.");

        Puntaje = puntaje;
        Comentario = Guardas.TextoOpcional(comentario);
        FechaHora = fechaHora;
    }

    public Guid Id { get; }
    public Guid ViajeId { get; }
    public Guid PasajeroId { get; }
    public int Puntaje { get; }
    public string? Comentario { get; }
    public DateTimeOffset FechaHora { get; }
}
