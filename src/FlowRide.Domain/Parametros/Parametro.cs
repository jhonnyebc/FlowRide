namespace FlowRide.Domain.Parametros;

/// <summary>
/// Valor de un parámetro que el Administrador cambió (ADR-0016). Si no hay fila, rige el valor por defecto de la
/// definición (versión 0). Cada cambio incrementa la versión.
/// </summary>
public sealed class Parametro
{
    public Parametro(DefinicionParametro definicion, string valorJson, Guid? modificadoPorId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        Clave = definicion.Clave;
        Tipo = definicion.Tipo;
        Valor = definicion.Normalizar(valorJson);
        Version = 1;
        ModificadoPorId = modificadoPorId;
        ModificadoEn = ahora;
    }

#pragma warning disable CS8618 // Solo para EF Core.
    private Parametro()
    {
    }
#pragma warning restore CS8618

    public string Clave { get; private set; }
    public TipoParametro Tipo { get; private set; }

    /// <summary>JSON canónico del valor.</summary>
    public string Valor { get; private set; }

    public int Version { get; private set; }
    public Guid? ModificadoPorId { get; private set; }
    public DateTimeOffset ModificadoEn { get; private set; }

    /// <summary>Devuelve false si el valor no cambia (no sube la versión).</summary>
    public bool Cambiar(DefinicionParametro definicion, string valorJson, Guid? modificadoPorId, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(definicion);
        var nuevo = definicion.Normalizar(valorJson);
        if (nuevo == Valor)
            return false;

        Valor = nuevo;
        Version++;
        ModificadoPorId = modificadoPorId;
        ModificadoEn = ahora;
        return true;
    }
}
