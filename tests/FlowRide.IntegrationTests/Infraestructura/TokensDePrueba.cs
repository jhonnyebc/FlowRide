using System.Security.Cryptography;
using FlowRide.Infrastructure.Identidad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Emisor OIDC de prueba: firma tokens con una clave local para probar la autorización de la API sin Keycloak.
/// La API valida firma, emisor, audiencia y vigencia exactamente igual que con el realm real.
/// </summary>
public static class TokensDePrueba
{
    public const string Emisor = "https://idp.pruebas.flowride.local/realms/flowride-tenant-a";

    private static readonly SymmetricSecurityKey Clave = new(RandomNumberGenerator.GetBytes(32)) { KeyId = "clave-de-prueba" };

    public static string Crear(
        string sub,
        string? tenantId = "tenant-a",
        string audiencia = ConstantesKeycloak.Audiencia,
        DateTime? expira = null,
        SecurityKey? clave = null,
        DateTimeOffset? autenticadoEn = null)
    {
        // auth_time: cuándo inició sesión (las acciones sensibles exigen que sea reciente).
        var claims = new Dictionary<string, object>
        {
            ["sub"] = sub,
            ["auth_time"] = (autenticadoEn ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
        };
        if (tenantId is not null) claims[ConstantesKeycloak.ClaimTenant] = tenantId;

        var vence = expira ?? DateTime.UtcNow.AddMinutes(5);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = audiencia,
            Claims = claims,
            NotBefore = vence.AddMinutes(-10),
            IssuedAt = vence.AddMinutes(-10),
            Expires = vence,
            SigningCredentials = new SigningCredentials(clave ?? Clave, SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>Metadatos OIDC del emisor de prueba (sin red), para quien valide los tokens fuera de la API (el borde).</summary>
    public static IConfigurationManager<OpenIdConnectConfiguration> Configuracion()
    {
        var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
        configuracion.SigningKeys.Add(Clave);
        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
    }

    /// <summary>Cambia los metadatos OIDC del realm por los del emisor de prueba (sin red).</summary>
    public static void Usar(IServiceCollection servicios) =>
        servicios.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opciones =>
        {
            var configuracion = new OpenIdConnectConfiguration { Issuer = Emisor };
            configuracion.SigningKeys.Add(Clave);
            opciones.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
            opciones.TokenValidationParameters.ValidIssuer = Emisor;
            opciones.TokenValidationParameters.IssuerSigningKey = Clave;
            opciones.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        });
}
