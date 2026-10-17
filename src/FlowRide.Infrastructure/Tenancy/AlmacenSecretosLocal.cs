using System.Text.Json;
using FlowRide.Application.Tenancy.Salida.Puertos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>
/// Equivalente local de un Key Vault por operador (ADR-0014): cada ámbito es un almacén de <i>user-secrets</i>
/// propio (<c>flowride-&lt;tenant&gt;</c>), fuera del repositorio. En la nube se reemplaza por Azure Key Vault.
/// </summary>
public sealed class AlmacenSecretosLocal : IAlmacenSecretos
{
    private static readonly JsonSerializerOptions Formato = new() { WriteIndented = true };
    private readonly string? _raiz;

    /// <param name="raiz">Carpeta base; si es null se usa la de user-secrets del usuario (solo cambia en pruebas).</param>
    public AlmacenSecretosLocal(string? raiz = null) => _raiz = raiz;

    public static string RutaPorDefecto(string ambito) => PathHelper.GetSecretsPathFromSecretsId(ambito);

    public string Ruta(string ambito)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ambito);
        return _raiz is null ? RutaPorDefecto(ambito) : Path.Combine(_raiz, ambito, "secrets.json");
    }

    public async Task<string?> ObtenerAsync(string ambito, string clave, CancellationToken ct)
    {
        var secretos = await LeerAsync(Ruta(ambito), ct);
        return secretos.TryGetValue(clave, out var valor) ? valor : null;
    }

    public async Task GuardarAsync(string ambito, string clave, string valor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        ArgumentException.ThrowIfNullOrWhiteSpace(valor);

        var ruta = Ruta(ambito);
        var secretos = await LeerAsync(ruta, ct);
        secretos[clave] = valor;

        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        var temporal = ruta + ".tmp";
        await File.WriteAllTextAsync(temporal, JsonSerializer.Serialize(secretos, Formato), ct);
        File.Move(temporal, ruta, overwrite: true);
    }

    private static async Task<SortedDictionary<string, string>> LeerAsync(string ruta, CancellationToken ct)
    {
        if (!File.Exists(ruta))
            return new SortedDictionary<string, string>(StringComparer.Ordinal);

        await using var archivo = File.OpenRead(ruta);
        var leidos = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(archivo, cancellationToken: ct);
        return new SortedDictionary<string, string>(leidos ?? [], StringComparer.Ordinal);
    }
}

public static class SecretosLocalesExtensions
{
    /// <summary>
    /// Agrega los secretos locales del operador del stack (<c>FlowRide:TenantId</c>). Solo para desarrollo:
    /// en la nube el stack lee su Key Vault (ADR-0014). Si el TenantId falta o es inválido no agrega nada;
    /// la validación de arranque se encarga de rechazarlo.
    /// Precedencia: por encima de appsettings y user-secrets del proyecto, pero por debajo de las variables de
    /// entorno y de la línea de comandos, que siguen pudiendo sobrescribirlo.
    /// </summary>
    public static IConfigurationBuilder AddSecretosLocalesDelOperador(
        this IConfigurationBuilder configuracion,
        string? tenantId,
        string? raiz = null)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        if (!Domain.Plataforma.Operador.EsIdValido(tenantId))
            return configuracion;

        var ambito = Domain.Plataforma.Operador.AmbitoSecretosDe(tenantId!);
        configuracion.AddJsonFile(new AlmacenSecretosLocal(raiz).Ruta(ambito), optional: true, reloadOnChange: false);

        var fuentes = configuracion.Sources;
        var indiceEntorno = IndiceDeVariablesDeEntorno(fuentes);
        if (indiceEntorno >= 0)
        {
            var agregada = fuentes[^1];
            fuentes.RemoveAt(fuentes.Count - 1);
            fuentes.Insert(indiceEntorno, agregada);
        }

        return configuracion;
    }

    /// <summary>
    /// Posición de la fuente general de variables de entorno (sin prefijo). La de prefijo <c>DOTNET_</c> o
    /// <c>ASPNETCORE_</c> que el host agrega al principio no cuenta: quedar debajo de ella dejaría el almacén
    /// por debajo de appsettings.
    /// </summary>
    private static int IndiceDeVariablesDeEntorno(IList<IConfigurationSource> fuentes)
    {
        for (var i = fuentes.Count - 1; i >= 0; i--)
        {
            if (fuentes[i] is Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationSource fuente
                && string.IsNullOrEmpty(fuente.Prefix))
                return i;
        }
        return -1;
    }
}
