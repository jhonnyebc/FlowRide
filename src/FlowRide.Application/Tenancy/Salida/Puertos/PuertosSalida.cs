namespace FlowRide.Application.Tenancy.Salida.Puertos;

// Puertos de salida del aprovisionamiento que no son repositorios: los implementa Infrastructure.
// El repositorio de operadores es un puerto del dominio (IOperadorRepositorio).

/// <summary>Servidor PostgreSQL donde viven las bases de los Silos (ADR-0010 §5).</summary>
public interface IServidorSilos
{
    /// <summary>
    /// Crea el rol y la base del Silo si no existen y deja la contraseña del rol igual a la indicada.
    /// Ningún otro rol puede conectarse a esa base. Es idempotente.
    /// </summary>
    Task AsegurarSiloAsync(string nombreSilo, string contrasena, CancellationToken ct);

    /// <summary>Cadena de conexión del rol propio del Silo hacia su base.</summary>
    string CadenaConexion(string nombreSilo, string contrasena);
}

/// <summary>Aplica las migraciones del esquema operativo a la base de un Silo.</summary>
public interface IMigradorSilo
{
    /// <summary>Devuelve las migraciones aplicadas en esta ejecución (vacío si el esquema ya estaba al día).</summary>
    Task<IReadOnlyList<string>> MigrarAsync(string cadenaConexion, CancellationToken ct);
}

/// <summary>Secretos con un ámbito por operador: ninguna credencial se comparte entre operadores (ADR-0014).</summary>
public interface IAlmacenSecretos
{
    Task<string?> ObtenerAsync(string ambito, string clave, CancellationToken ct);

    Task GuardarAsync(string ambito, string clave, string valor, CancellationToken ct);
}

/// <summary>Genera secretos aleatorios criptográficamente seguros.</summary>
public interface IGeneradorSecretos
{
    string NuevaContrasena();
}

/// <summary>Claves de los secretos del stack de un operador.</summary>
public static class ClavesSecretos
{
    /// <summary>Misma clave que lee la API: <c>ConnectionStrings:Silo</c>.</summary>
    public const string CadenaConexionSilo = "ConnectionStrings:Silo";

    public const string ContrasenaSilo = "FlowRide:Silo:Contrasena";
}
