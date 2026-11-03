using System.Text.Json;
using System.Text.Json.Nodes;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Contrato;

/// <summary>
/// El Panel Web genera su cliente tipado desde <c>web/src/api/openapi.json</c> (ADR-0019). Esta prueba falla si la API
/// cambió y ese archivo no: así el frontend no se desincroniza. Para regenerarlo:
/// <c>FLOWRIDE_ACTUALIZAR_CONTRATO=1 dotnet test tests/FlowRide.IntegrationTests --filter ContratoOpenApi</c> y luego
/// <c>npm run api:tipos</c> en <c>web/</c>.
/// </summary>
[Trait("Category", "Integration")]
public class ContratoOpenApiTests
{
    private const string VariableActualizar = "FLOWRIDE_ACTUALIZAR_CONTRATO";
    private static readonly JsonSerializerOptions Legible = new() { WriteIndented = true };

    [Fact]
    public async Task ElContratoDelFrontend_EsElDeLaApi()
    {
        await using var api = new FlowRideApiFactory("Host=localhost;Port=1;Database=sin_conexion;Username=x;Password=x");
        var documento = JsonNode.Parse(await api.CreateClient().GetStringAsync("/openapi/v1.json"))!;
        documento.AsObject().Remove("servers"); // depende del host de la petición, no del contrato
        var actual = documento.ToJsonString(Legible).ReplaceLineEndings("\n") + "\n";

        var archivo = Path.Combine(RaizDelRepositorio(), "web", "src", "api", "openapi.json");
        if (Environment.GetEnvironmentVariable(VariableActualizar) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(archivo)!);
            await File.WriteAllTextAsync(archivo, actual);
            return;
        }

        Assert.True(File.Exists(archivo), $"Falta {archivo}: genéralo con {VariableActualizar}=1.");
        var guardado = (await File.ReadAllTextAsync(archivo)).ReplaceLineEndings("\n");
        Assert.True(guardado == actual,
            $"La API cambió y web/src/api/openapi.json no. Regenéralo con {VariableActualizar}=1 y corre 'npm run api:tipos' en web/.");
        Assert.Contains("/api/reportes/vista-centralizada", actual, StringComparison.Ordinal);
    }

    private static string RaizDelRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null && !File.Exists(Path.Combine(directorio.FullName, "FlowRide.slnx")))
            directorio = directorio.Parent;
        return directorio?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (FlowRide.slnx).");
    }
}
