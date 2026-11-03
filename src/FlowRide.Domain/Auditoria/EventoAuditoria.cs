using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FlowRide.Domain.Auditoria;

/// <summary>
/// Evento de la auditoría legal del Silo (ADR-0011): quién hizo qué, sobre qué recurso, cuándo y con qué resultado.
/// Solo se inserta. Cada evento se encadena con el anterior por su hash (SHA-256), así que alterar o borrar uno
/// rompe la cadena desde ese punto y se detecta al verificarla (<see cref="CadenaAuditoria"/>).
/// Los valores antes/después no llevan datos personales: el archivo de auditoría no los conserva (ADR-0013).
/// </summary>
public sealed class EventoAuditoria
{
    /// <summary>Hash previo del primer evento de la cadena.</summary>
    public static readonly string HashInicial = new('0', 64);

    public EventoAuditoria(
        Guid? actorId,
        string accion,
        string recurso,
        string recursoId,
        DateTimeOffset fechaHora,
        ResultadoAuditoria resultado,
        string? motivo = null,
        IReadOnlyDictionary<string, string?>? valoresAntes = null,
        IReadOnlyDictionary<string, string?>? valoresDespues = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);
        ArgumentException.ThrowIfNullOrWhiteSpace(recurso);
        ArgumentException.ThrowIfNullOrWhiteSpace(recursoId);

        ActorId = actorId;
        Accion = accion.Trim();
        Recurso = recurso.Trim();
        RecursoId = recursoId.Trim();
        // PostgreSQL guarda microsegundos: se trunca aquí para que el hash calculado y el releído coincidan.
        FechaHora = new DateTimeOffset(fechaHora.UtcTicks - fechaHora.UtcTicks % 10, TimeSpan.Zero);
        Resultado = resultado;
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        ValoresAntes = valoresAntes is null ? null : JsonCanonico(valoresAntes);
        ValoresDespues = valoresDespues is null ? null : JsonCanonico(valoresDespues);
    }

#pragma warning disable CS8618 // Solo para EF Core: rellena las propiedades al materializar.
    private EventoAuditoria()
    {
    }
#pragma warning restore CS8618

    /// <summary>Secuencia que asigna la base (bigint identity): da el orden de la cadena.</summary>
    public long Id { get; private set; }

    public Guid? ActorId { get; private set; }
    public string Accion { get; private set; }
    public string Recurso { get; private set; }
    public string RecursoId { get; private set; }
    public DateTimeOffset FechaHora { get; private set; }
    public ResultadoAuditoria Resultado { get; private set; }
    public string? Motivo { get; private set; }

    /// <summary>JSON plano con las claves ordenadas.</summary>
    public string? ValoresAntes { get; private set; }

    public string? ValoresDespues { get; private set; }

    public string HashPrevio { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;

    public bool Sellado => Hash.Length == 64;

    /// <summary>Encadena el evento con el último de la cadena. Se hace una vez, justo antes de insertarlo.</summary>
    public void Sellar(string hashPrevio)
    {
        if (Sellado)
            throw new InvalidOperationException("El evento de auditoría ya está sellado.");
        if (hashPrevio is not { Length: 64 })
            throw new ArgumentException("El hash previo debe ser un SHA-256 en hexadecimal.", nameof(hashPrevio));

        HashPrevio = hashPrevio;
        Hash = CalcularHash(hashPrevio);
    }

    /// <summary>El hash guardado corresponde al contenido actual del evento.</summary>
    public bool HashValido() => Sellado && Hash == CalcularHash(HashPrevio);

    private string CalcularHash(string hashPrevio)
    {
        var contenido = string.Join('\n',
            hashPrevio,
            ActorId?.ToString("D") ?? string.Empty,
            Accion,
            Recurso,
            RecursoId,
            FechaHora.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture),
            Resultado.ToString(),
            Motivo ?? string.Empty,
            Normalizar(ValoresAntes),
            Normalizar(ValoresDespues));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));
    }

    // jsonb reordena claves y cambia espacios al guardar: el hash se calcula sobre la forma canónica.
    private static string Normalizar(string? json) =>
        json is null ? string.Empty : JsonCanonico(JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? []);

    private static string JsonCanonico(IReadOnlyDictionary<string, string?> valores) =>
        JsonSerializer.Serialize(new SortedDictionary<string, string?>(
            valores.ToDictionary(par => par.Key, par => par.Value, StringComparer.Ordinal), StringComparer.Ordinal));
}
