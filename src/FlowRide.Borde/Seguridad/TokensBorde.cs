using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using FlowRide.Infrastructure.Identidad;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Model;

namespace FlowRide.Borde.Seguridad;

/// <summary>Emisor y claves públicas del realm de un operador (ADR-0003: un realm por operador).</summary>
public interface IEmisoresTokens
{
    (string Emisor, IConfigurationManager<OpenIdConnectConfiguration> Claves) DelOperador(string tenantId);
}

/// <summary>Lee de Keycloak los metadatos OIDC de cada realm y los guarda en caché (rota las claves solo).</summary>
public sealed class EmisoresKeycloak(IdentidadOptions opciones) : IEmisoresTokens
{
    private readonly ConcurrentDictionary<string, (string, IConfigurationManager<OpenIdConnectConfiguration>)> _porOperador =
        new(StringComparer.Ordinal);

    public (string Emisor, IConfigurationManager<OpenIdConnectConfiguration> Claves) DelOperador(string tenantId) =>
        _porOperador.GetOrAdd(tenantId, id =>
        {
            var autoridad = opciones.AutoridadDelOperador(id);
            return (autoridad, new ConfigurationManager<OpenIdConnectConfiguration>($"{autoridad}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = opciones.RequiereHttps }));
        });
}

public enum ResultadoToken
{
    Valido,
    Invalido,
    SinClaves
}

/// <summary>
/// La parte de autenticación del API gateway (decisión del 2026-10-20, video de reverse proxy vs API gateway): el borde
/// solo reenvía a <c>/api/</c> peticiones con un token vigente del realm del operador de ese subdominio. El stack lo
/// vuelve a validar y además resuelve el usuario interno (defensa en profundidad).
/// </summary>
public sealed class ValidadorTokensBorde(IEmisoresTokens emisores)
{
    private static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(30);
    private readonly JsonWebTokenHandler _manejador = new();

    public async Task<ResultadoToken> ValidarAsync(string? autorizacion, string tenantId, CancellationToken ct)
    {
        const string Prefijo = "Bearer ";
        if (autorizacion is null || !autorizacion.StartsWith(Prefijo, StringComparison.OrdinalIgnoreCase))
            return ResultadoToken.Invalido;

        var (emisor, claves) = emisores.DelOperador(tenantId);
        OpenIdConnectConfiguration configuracion;
        try
        {
            configuracion = await claves.GetConfigurationAsync(ct);
        }
        catch (InvalidOperationException)
        {
            return ResultadoToken.SinClaves;
        }

        var resultado = await _manejador.ValidateTokenAsync(autorizacion[Prefijo.Length..].Trim(), new TokenValidationParameters
        {
            ValidIssuer = emisor,
            ValidAudience = ConstantesKeycloak.Audiencia,
            IssuerSigningKeys = configuracion.SigningKeys,
            ClockSkew = Tolerancia
        });
        return resultado.IsValid
               && resultado.Claims.TryGetValue(ConstantesKeycloak.ClaimTenant, out var tenant)
               && string.Equals(tenant?.ToString(), tenantId, StringComparison.Ordinal)
            ? ResultadoToken.Valido
            : ResultadoToken.Invalido;
    }
}

public static partial class TokensBorde
{
    /// <summary>Metadato de la ruta de YARP con el operador al que lleva.</summary>
    public const string MetadatoOperador = "operador";

    private static readonly Meter Medidor = new("FlowRide.Borde.Tokens");
    private static readonly Counter<long> Rechazados = Medidor.CreateCounter<long>("flowride.borde.tokens_rechazados",
        description: "Peticiones a la API de un operador rechazadas en el borde por no traer un token válido de su realm.");

    /// <summary>
    /// En el pipeline del proxy: a <c>/api/</c> solo pasa un token válido del operador de la ruta. Las preflight de CORS
    /// (OPTIONS) y lo que no es API (por ejemplo, la salud del stack) no lo necesitan.
    /// </summary>
    public static void UseTokenDelOperador(this IReverseProxyApplicationBuilder proxy) =>
        proxy.Use(async (contexto, siguiente) =>
        {
            var peticion = contexto.Request;
            var operador = contexto.GetReverseProxyFeature().Route.Config.Metadata?.GetValueOrDefault(MetadatoOperador);
            if (operador is null || HttpMethods.IsOptions(peticion.Method)
                || !peticion.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                await siguiente();
                return;
            }

            var resultado = await contexto.RequestServices.GetRequiredService<ValidadorTokensBorde>()
                .ValidarAsync(peticion.Headers.Authorization.FirstOrDefault(), operador, contexto.RequestAborted);
            if (resultado == ResultadoToken.Valido)
            {
                await siguiente();
                return;
            }

            Rechazados.Add(1);
            var registro = contexto.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(TokensBorde));
            if (resultado == ResultadoToken.SinClaves)
            {
                ClavesNoDisponibles(registro, operador);
                contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
            contexto.Response.Headers.WWWAuthenticate = "Bearer";
        });

    [LoggerMessage(Level = LogLevel.Error, Message = "No se pudieron leer las claves del realm del operador {Operador}.")]
    private static partial void ClavesNoDisponibles(ILogger registro, string operador);
}
