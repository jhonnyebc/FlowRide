using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlowRide.Application.Tenancy.Salida.Puertos;
using Microsoft.Extensions.Configuration;

namespace FlowRide.Infrastructure.Seguridad;

/// <summary>
/// Cifrado a nivel de aplicación de los campos sensibles (teléfono y licencia, ADR-0014) con la clave propia del
/// operador. Es <b>determinista</b>: el IV se deriva del contenido con HMAC-SHA256 y se cifra con AES-256-GCM, así que
/// un mismo valor siempre da el mismo texto cifrado. Eso permite el índice único y la búsqueda por valor exacto (el
/// bot de WhatsApp busca por número); solo revela si dos valores son iguales.
/// Formato: <c>v1.&lt;base64url(iv | cifrado | etiqueta)&gt;</c>. La versión permite rotar: se descifra con la versión
/// guardada y se cifra con la vigente.
/// </summary>
public sealed partial class CifradorCampos
{
    private const int TamanoIv = 12;
    private const int TamanoEtiqueta = 16;
    private const int LargoMinimoClave = 32;

    private readonly Dictionary<string, (byte[] Cifrado, byte[] Mac)> _claves;

    public CifradorCampos(IReadOnlyDictionary<string, string> clavesMaestras, string versionVigente)
    {
        ArgumentNullException.ThrowIfNull(clavesMaestras);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionVigente);

        _claves = new Dictionary<string, (byte[], byte[])>(StringComparer.Ordinal);
        foreach (var (version, maestra) in clavesMaestras)
        {
            if (!PatronVersion().IsMatch(version))
                throw new InvalidOperationException($"La versión de clave '{version}' no es válida (usa v1, v2...).");
            if (maestra is null || maestra.Length < LargoMinimoClave)
                throw new InvalidOperationException($"La clave de cifrado {version} debe tener al menos {LargoMinimoClave} caracteres.");

            var material = Encoding.UTF8.GetBytes(maestra);
            _claves[version] = (
                HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, info: "FlowRide/campos/aes-gcm"u8.ToArray()),
                HKDF.DeriveKey(HashAlgorithmName.SHA256, material, 32, info: "FlowRide/campos/hmac-iv"u8.ToArray()));
        }

        if (!_claves.ContainsKey(versionVigente))
            throw new InvalidOperationException($"No hay clave de cifrado para la versión vigente '{versionVigente}'.");
        VersionVigente = versionVigente;
    }

    public string VersionVigente { get; }

    /// <summary>Lee las claves del operador de su configuración (almacén de secretos). Falla si faltan (ADR-0014).</summary>
    public static CifradorCampos DesdeConfiguracion(IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        var seccion = ClavesSecretos.ClaveCifradoV1[..ClavesSecretos.ClaveCifradoV1.LastIndexOf(':')];
        var claves = configuracion.GetSection(seccion).GetChildren()
            .Where(clave => !string.IsNullOrWhiteSpace(clave.Value))
            .ToDictionary(clave => clave.Key, clave => clave.Value!, StringComparer.Ordinal);
        var vigente = configuracion[ClavesSecretos.VersionCifradoVigente];
        if (claves.Count == 0 || string.IsNullOrWhiteSpace(vigente))
            throw new InvalidOperationException(
                $"Falta el secreto '{ClavesSecretos.ClaveCifradoV1}' o '{ClavesSecretos.VersionCifradoVigente}'. " +
                "Lo genera la herramienta de aprovisionamiento en el almacén de secretos del operador.");
        return new CifradorCampos(claves, vigente);
    }

    public string Cifrar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);
        var (claveCifrado, claveMac) = _claves[VersionVigente];
        var plano = Encoding.UTF8.GetBytes(texto);

        var salida = new byte[TamanoIv + plano.Length + TamanoEtiqueta];
        var iv = salida.AsSpan(0, TamanoIv);
        HMACSHA256.HashData(claveMac, plano).AsSpan(0, TamanoIv).CopyTo(iv);
        using var aes = new AesGcm(claveCifrado, TamanoEtiqueta);
        aes.Encrypt(iv, plano, salida.AsSpan(TamanoIv, plano.Length), salida.AsSpan(TamanoIv + plano.Length));

        return $"{VersionVigente}.{Base64Url.EncodeToString(salida)}";
    }

    /// <summary>Descifra con la versión del valor. Un valor sin prefijo de versión se considera en claro (datos anteriores).</summary>
    public string Descifrar(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        var partes = PatronCifrado().Match(valor);
        if (!partes.Success)
            return valor;

        if (!_claves.TryGetValue(partes.Groups[1].Value, out var claves))
            throw new InvalidOperationException($"No hay clave para descifrar la versión '{partes.Groups[1].Value}'.");

        var datos = Base64Url.DecodeFromChars(partes.Groups[2].Value);
        if (datos.Length < TamanoIv + TamanoEtiqueta)
            throw new CryptographicException("El valor cifrado está incompleto.");

        var largo = datos.Length - TamanoIv - TamanoEtiqueta;
        var plano = new byte[largo];
        using var aes = new AesGcm(claves.Cifrado, TamanoEtiqueta);
        aes.Decrypt(datos.AsSpan(0, TamanoIv), datos.AsSpan(TamanoIv, largo), datos.AsSpan(TamanoIv + largo), plano);
        return Encoding.UTF8.GetString(plano);
    }

    public bool EstaCifradoConVigente(string valor) =>
        valor.StartsWith(VersionVigente + ".", StringComparison.Ordinal);

    [GeneratedRegex(@"^v\d+$")]
    private static partial Regex PatronVersion();

    [GeneratedRegex(@"^(v\d+)\.([A-Za-z0-9_-]+)$")]
    private static partial Regex PatronCifrado();
}
