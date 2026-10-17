using System.Security.Claims;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Application.Identidad.Entrada.Puertos;
using FlowRide.Infrastructure.Identidad;
using FlowRide.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace FlowRide.API.Identidad;

/// <summary>Políticas de autorización (RBAC, RF-02). El rol sale del usuario interno, no del token (ADR-0003).</summary>
public static class Politicas
{
    public const string Administrador = "Administrador";
    public const string PanelWeb = "PanelWeb";
}

/// <summary>Claims que la API agrega tras resolver el usuario interno.</summary>
public static class ClaimsFlowRide
{
    public const string UsuarioId = "flowride:usuario_id";
    public const string Esquema = "flowride";

    /// <summary>Usuario interno autenticado (el actor de la auditoría); null si no hay sesión.</summary>
    public static Guid? UsuarioActual(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal?.FindFirstValue(UsuarioId), out var id) ? id : null;
}

/// <summary>
/// Autenticación OIDC del Panel Web contra el realm del operador (ADR-0003) y autorización por roles. Cada petición:
/// 1) valida firma, emisor, audiencia y vigencia del token; 2) exige que <c>tenant_id</c> sea el operador de este
/// stack (ADR-0002); 3) busca el usuario interno por su <c>sub</c> y lo rechaza si no existe o está inactivo, sin
/// caché, para que la revocación sea inmediata (ADM-SEG-01).
/// </summary>
public static partial class AutenticacionExtensions
{
    public static IServiceCollection AddAutenticacionFlowRide(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<IdentidadOptions>, IOptions<SiloOptions>>((opciones, identidad, silo) =>
            {
                opciones.Authority = identidad.Value.AutoridadDelOperador(silo.Value.TenantId);
                opciones.Audience = ConstantesKeycloak.Audiencia;
                opciones.RequireHttpsMetadata = identidad.Value.RequiereHttps;
                opciones.MapInboundClaims = false;
                opciones.Events = new JwtBearerEvents { OnTokenValidated = ValidarContraFlowRideAsync };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Administrador, politica => politica.RequireRole(RolesPanel.Administrador))
            .AddPolicy(Politicas.PanelWeb, politica => politica.RequireRole(RolesPanel.Administrador, RolesPanel.OperadorLogistico));

        return services;
    }

    private static async Task ValidarContraFlowRideAsync(TokenValidatedContext contexto)
    {
        var servicios = contexto.HttpContext.RequestServices;
        var logger = servicios.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AutenticacionExtensions).FullName!);
        var principal = contexto.Principal!;

        // ADR-0002: un token válido de otro operador no abre este stack.
        var tenantEsperado = servicios.GetRequiredService<IOptions<SiloOptions>>().Value.TenantId;
        if (principal.FindFirstValue(ConstantesKeycloak.ClaimTenant) != tenantEsperado)
        {
            TokenDeOtroOperador(logger);
            contexto.Fail("El token no pertenece a este operador.");
            return;
        }

        // ADR-0003: el token identifica; la decisión la toma FlowRide con su usuario interno.
        var identidadExterna = principal.FindFirstValue("sub");
        var usuario = identidadExterna is null
            ? null
            : await servicios.GetRequiredService<IResolverIdentidadPuertoEntrada>()
                .ResolverAsync(identidadExterna, contexto.HttpContext.RequestAborted);
        if (usuario is null)
        {
            UsuarioSinAcceso(logger);
            contexto.Fail("El usuario no existe en FlowRide o está inactivo.");
            return;
        }

        var claims = new List<Claim>
        {
            new(ClaimsFlowRide.UsuarioId, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Role, usuario.Rol)
        };
        if (usuario.Correo is not null) claims.Add(new Claim(ClaimTypes.Email, usuario.Correo));
        principal.AddIdentity(new ClaimsIdentity(claims, ClaimsFlowRide.Esquema, ClaimTypes.Name, ClaimTypes.Role));
    }

    // Sin el token ni datos personales en el log (ADR-0011).
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "Acceso denegado: el token pertenece a otro operador.")]
    private static partial void TokenDeOtroOperador(ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Acceso denegado: el usuario no existe en FlowRide o está inactivo.")]
    private static partial void UsuarioSinAcceso(ILogger logger);
}
