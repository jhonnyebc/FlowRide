namespace FlowRide.Domain.ValoresObjeto;

/// <summary>Intervalo de tiempo semiabierto [Inicio, Fin).</summary>
public sealed record FranjaHoraria
{
    public FranjaHoraria(DateTimeOffset inicio, DateTimeOffset fin)
    {
        if (fin <= inicio)
            throw new ArgumentException("El fin de la franja debe ser posterior a su inicio.", nameof(fin));

        Inicio = inicio;
        Fin = fin;
    }

    public DateTimeOffset Inicio { get; }
    public DateTimeOffset Fin { get; }

    public TimeSpan Duracion => Fin - Inicio;

    public bool Contiene(DateTimeOffset instante) => Inicio <= instante && instante < Fin;

    public bool SeSolapaCon(FranjaHoraria otra)
    {
        ArgumentNullException.ThrowIfNull(otra);
        return Inicio < otra.Fin && otra.Inicio < Fin;
    }

    public bool EstaContenidaEn(FranjaHoraria otra)
    {
        ArgumentNullException.ThrowIfNull(otra);
        return otra.Inicio <= Inicio && Fin <= otra.Fin;
    }
}
