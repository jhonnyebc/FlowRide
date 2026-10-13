using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>
/// Agrega al final del pipeline una ruta que lanza una excepción, para probar el manejo de errores
/// sin exponer endpoints de prueba en el código de producción.
/// </summary>
public sealed class RutaDeErrorDePrueba : IStartupFilter
{
    public const string Ruta = "/__pruebas/error";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Map(Ruta, rama => rama.Run(_ => throw new InvalidOperationException("detalle-interno-que-no-debe-filtrarse")));
    };
}
