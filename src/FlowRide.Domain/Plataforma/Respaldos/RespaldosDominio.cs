using System.Globalization;

namespace FlowRide.Domain.Plataforma.Respaldos;

// Respaldos de la base de cada operador en su propio bucket (ADR-0013): datos de entrada, resultados y retención.

/// <summary>Un respaldo guardado: nombre del objeto, cuándo se tomó y su tamaño.</summary>
public sealed record Respaldo(string Nombre, DateTimeOffset TomadoEn, long TamanoBytes);

/// <summary>Guardar un respaldo ya generado (pg_dump) del Silo de un operador.</summary>
public sealed record GuardarRespaldoDominio(string OperadorId, Stream Contenido);

/// <summary>El respaldo nuevo y los que se borraron por retención.</summary>
public sealed record ResultadoRespaldo(Respaldo Guardado, IReadOnlyList<Respaldo> Eliminados);

public sealed record ConsultarRespaldosDominio(string OperadorId);

/// <summary>Descargar un respaldo; sin nombre, el más reciente.</summary>
public sealed record DescargarRespaldoDominio(string OperadorId, string? Nombre, Stream Destino);

/// <summary>
/// Retención de los respaldos (ADR-0013): se conservan 35 días y nunca se borra el más reciente, aunque sea más viejo,
/// para que un operador sin respaldos nuevos no se quede sin ninguno.
/// </summary>
public static class PoliticaRetencionRespaldos
{
    public const int DiasRetencion = 35;

    /// <summary>Carpeta de los respaldos dentro del bucket del operador.</summary>
    public const string Prefijo = "respaldos/";

    /// <summary>Nombre del objeto: ordena igual alfabética y cronológicamente (UTC).</summary>
    public static string NombrePara(DateTimeOffset instante) =>
        $"{Prefijo}silo-{instante.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)}.dump";

    public static IReadOnlyList<Respaldo> Vencidos(IEnumerable<Respaldo> respaldos, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(respaldos);
        var limite = ahora.AddDays(-DiasRetencion);
        return respaldos
            .OrderByDescending(respaldo => respaldo.TomadoEn)
            .Skip(1)
            .Where(respaldo => respaldo.TomadoEn < limite)
            .ToList();
    }
}
