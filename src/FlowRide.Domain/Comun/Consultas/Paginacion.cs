namespace FlowRide.Domain.Comun.Consultas;

/// <summary>Página pedida por un listado (RF-41). Se normaliza: página ≥ 1 y tamaño entre 1 y <see cref="TamanoMaximo"/>.</summary>
public sealed record Paginacion
{
    public const int TamanoPorDefecto = 50;
    public const int TamanoMaximo = 200;

    public Paginacion(int? pagina = null, int? tamano = null)
    {
        Pagina = Math.Max(1, pagina ?? 1);
        Tamano = Math.Clamp(tamano ?? TamanoPorDefecto, 1, TamanoMaximo);
    }

    public int Pagina { get; }
    public int Tamano { get; }
    public int Salto => (Pagina - 1) * Tamano;

    public static Paginacion PorDefecto { get; } = new();
}

/// <summary>Elementos de una página y el total que cumple el filtro.</summary>
public sealed record PaginaResultado<T>(IReadOnlyList<T> Elementos, int Total, Paginacion Paginacion)
{
    public PaginaResultado<TDestino> Convertir<TDestino>(Func<T, TDestino> conversion) =>
        new(Elementos.Select(conversion).ToList(), Total, Paginacion);
}
