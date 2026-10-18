using FlowRide.Domain.Transversal;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.API.Plataforma.Errores;

/// <summary>
/// Convierte en ProblemDetails (RFC 9457) los errores que el usuario puede resolver: una regla de negocio incumplida
/// (<see cref="FlowRideExcepcion"/>) es 400 con su mensaje, y un conflicto de concurrencia optimista (ADR-0007), 409.
/// Las demás excepciones siguen el manejo por defecto: 500 sin detalles internos.
/// </summary>
public sealed class ManejadorExcepciones(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        var (estado, titulo, detalle) = exception switch
        {
            FlowRideExcepcion regla => (StatusCodes.Status400BadRequest, "La solicitud no cumple una regla de negocio.", regla.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "El recurso cambió mientras lo editabas.",
                "Otro usuario lo modificó al mismo tiempo; vuelve a consultarlo e intenta de nuevo."),
            _ => (0, string.Empty, string.Empty)
        };
        if (estado == 0)
            return false;

        httpContext.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = estado, Title = titulo, Detail = detalle }
        });
    }
}
