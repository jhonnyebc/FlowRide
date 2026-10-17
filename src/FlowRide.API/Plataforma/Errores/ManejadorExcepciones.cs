using FlowRide.Domain.Transversal;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FlowRide.API.Plataforma.Errores;

/// <summary>
/// Convierte una violación de regla de negocio (<see cref="FlowRideExcepcion"/>) en una respuesta 400 con su mensaje,
/// como ProblemDetails (RFC 9457). Las demás excepciones siguen el manejo por defecto: 500 sin detalles internos.
/// </summary>
public sealed class ManejadorExcepciones(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        if (exception is not FlowRideExcepcion regla)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "La solicitud no cumple una regla de negocio.",
                Detail = regla.Message
            }
        });
    }
}
