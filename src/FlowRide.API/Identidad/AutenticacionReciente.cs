using System.Globalization;
using FlowRide.Infrastructure.Identidad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace FlowRide.API.Identidad;

/// <summary>
/// Reautenticación para acciones sensibles (decisión del 2026-10-20, video OWASP de CSRF y autenticación rota): el último
/// inicio de sesión (claim <c>auth_time</c> del realm) debe ser reciente. Aunque roben una sesión abierta, no se puede
/// crear usuarios, cambiar roles, parámetros, plantillas ni la configuración del motor sin volver a autenticarse.
/// </summary>
public sealed class RequisitoAutenticacionReciente : IAuthorizationRequirement
{
    public const string ClaimAuthTime = "auth_time";
}

public sealed class ManejadorAutenticacionReciente(IOptions<IdentidadOptions> opciones, TimeProvider reloj)
    : AuthorizationHandler<RequisitoAutenticacionReciente>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequisitoAutenticacionReciente requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        var valor = context.User.FindFirst(RequisitoAutenticacionReciente.ClaimAuthTime)?.Value;
        if (long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var segundos)
            && reloj.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(segundos) <= Ventana(opciones.Value))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }

    public static TimeSpan Ventana(IdentidadOptions opciones) =>
        TimeSpan.FromMinutes(Math.Clamp(opciones.MinutosAutenticacionReciente, 1, 60));
}

/// <summary>
/// Si solo falta la autenticación reciente, responde 401 con el desafío de RFC 9470
/// (<c>error="insufficient_user_authentication"</c> y <c>max_age</c>) en lugar de 403: el Panel Web vuelve a pedir la
/// contraseña (y el TOTP) con <c>max_age</c> y repite la acción.
/// </summary>
public sealed class RespuestaAutenticacionReciente(IOptions<IdentidadOptions> opciones) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _porDefecto = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);
        var fallas = authorizeResult.AuthorizationFailure?.FailedRequirements.ToList() ?? [];
        if (!authorizeResult.Forbidden || fallas.Count == 0 || !fallas.All(f => f is RequisitoAutenticacionReciente))
            return _porDefecto.HandleAsync(next, context, policy, authorizeResult);

        var maximo = (int)ManejadorAutenticacionReciente.Ventana(opciones.Value).TotalSeconds;
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = string.Create(CultureInfo.InvariantCulture,
            $"Bearer error=\"insufficient_user_authentication\", error_description=\"Vuelve a iniciar sesión para esta acción.\", max_age=\"{maximo}\"");
        return Task.CompletedTask;
    }
}
