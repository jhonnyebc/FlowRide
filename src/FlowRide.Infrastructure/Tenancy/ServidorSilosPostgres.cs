using System.Text.RegularExpressions;
using FlowRide.Application.Tenancy.Salida.Puertos;
using Npgsql;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>
/// Crea el rol y la base de cada Silo en el servidor PostgreSQL, con una conexión administrativa de plataforma.
/// Cada base pertenece a su propio rol y ningún otro rol puede conectarse a ella (ADR-0002, ADR-0014).
/// </summary>
public sealed partial class ServidorSilosPostgres(NpgsqlDataSource administracion) : IServidorSilos
{
    public async Task AsegurarSiloAsync(string nombreSilo, string contrasena, CancellationToken ct)
    {
        var nombre = Identificador(nombreSilo);
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasena);
        var clave = Literal(contrasena);

        // El rol se crea o, si ya existe, toma la contraseña del secreto: el secreto es la fuente de verdad.
        var accionRol = await ExisteAsync("SELECT 1 FROM pg_roles WHERE rolname = $1", nombreSilo, ct)
            ? $"ALTER ROLE {nombre} WITH LOGIN PASSWORD {clave}"
            : $"CREATE ROLE {nombre} LOGIN PASSWORD {clave}";
        await EjecutarAsync(accionRol, ct);

        // CREATE DATABASE no admite transacción ni IF NOT EXISTS: se comprueba antes.
        if (!await ExisteAsync("SELECT 1 FROM pg_database WHERE datname = $1", nombreSilo, ct))
            await EjecutarAsync($"CREATE DATABASE {nombre} OWNER {nombre}", ct);

        await EjecutarAsync($"ALTER DATABASE {nombre} OWNER TO {nombre}", ct);
        await EjecutarAsync($"REVOKE ALL ON DATABASE {nombre} FROM PUBLIC", ct);
        await EjecutarAsync($"GRANT CONNECT, TEMPORARY ON DATABASE {nombre} TO {nombre}", ct);
    }

    public string CadenaConexion(string nombreSilo, string contrasena)
    {
        Identificador(nombreSilo);
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasena);

        // Mismo servidor, puerto y opciones de TLS que la conexión administrativa; credenciales propias del Silo.
        var builder = new NpgsqlConnectionStringBuilder(administracion.ConnectionString)
        {
            Database = nombreSilo,
            Username = nombreSilo,
            Password = contrasena
        };
        return builder.ConnectionString;
    }

    private async Task<bool> ExisteAsync(string consulta, string valor, CancellationToken ct)
    {
        await using var comando = administracion.CreateCommand(consulta);
        comando.Parameters.Add(new NpgsqlParameter { Value = valor });
        return await comando.ExecuteScalarAsync(ct) is not null;
    }

    private async Task EjecutarAsync(string sql, CancellationToken ct)
    {
        // Las sentencias DDL no admiten parámetros: los identificadores se validan y los literales se escapan.
        await using var comando = administracion.CreateCommand(sql);
        await comando.ExecuteNonQueryAsync(ct);
    }

    private static string Identificador(string nombreSilo)
    {
        if (string.IsNullOrEmpty(nombreSilo) || nombreSilo.Length > 63 || !PatronSilo().IsMatch(nombreSilo))
            throw new ArgumentException($"Nombre de Silo inválido: '{nombreSilo}'.", nameof(nombreSilo));
        return $"\"{nombreSilo}\"";
    }

    private static string Literal(string valor) => "'" + valor.Replace("'", "''", StringComparison.Ordinal) + "'";

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex PatronSilo();
}
