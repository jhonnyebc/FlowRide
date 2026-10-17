namespace FlowRide.Domain.Parametros;

/// <summary>Nuevo valor (JSON) de un parámetro, cambiado por el Administrador del operador.</summary>
public sealed record CambiarParametroDominio(string Clave, string ValorJson, Guid? ActorId);

/// <summary>Sin filtros: todos los parámetros con su valor vigente.</summary>
public sealed record ConsultarParametrosDominio
{
    public static ConsultarParametrosDominio Todos { get; } = new();
}

/// <summary>Valor que rige: el guardado o, si no hay, el de producto (versión 0).</summary>
public sealed record ParametroVigente(DefinicionParametro Definicion, Parametro? Guardado)
{
    public string Valor => Guardado?.Valor ?? Definicion.ValorPorDefecto;
    public int Version => Guardado?.Version ?? 0;
    public bool EsPorDefecto => Guardado is null;
}
