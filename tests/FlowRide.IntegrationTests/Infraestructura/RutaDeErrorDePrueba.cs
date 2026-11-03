using FlowRide.Domain.Transversal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Agrega al final del pipeline rutas que lanzan excepciones, para probar el manejo de errores
/// sin exponer endpoints de prueba en el código de producción.
/// </summary>
public sealed class RutaDeErrorDePrueba : IStartupFilter
{
    public const string Ruta = "/__pruebas/error";
    public const string RutaRegla = "/__pruebas/regla";
    public const string MensajeRegla = "El operador 'tenant-x' no está registrado en el catálogo.";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Map(Ruta, rama => rama.Run(_ => throw new InvalidOperationException("detalle-interno-que-no-debe-filtrarse")));
        app.Map(RutaRegla, rama => rama.Run(_ => throw new FlowRideExcepcion(MensajeRegla)));
    };
}
