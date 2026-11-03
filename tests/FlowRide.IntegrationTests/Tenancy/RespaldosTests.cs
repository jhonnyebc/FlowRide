using FlowRide.Aprovisionamiento;
using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Tenancy;

/// <summary>Respaldos del Silo en el bucket del operador (ADR-0013) con la herramienta real y Azurite real.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionAzurite.Nombre)]
public class RespaldosTests(PostgresFixture postgres, AzuriteFixture azurite)
{
    private static async Task<(int Codigo, string Salida)> EjecutarAsync(EntornoAprovisionamiento entorno, params string[] args)
    {
        Assert.True(Comando.TryParse(args, out var comando, out var error), error);
        using var salida = new StringWriter();
        var codigo = await entorno.UsarAsync(servicios => Comandos.EjecutarAsync(comando, servicios, salida, CancellationToken.None));
        return (codigo, salida.ToString());
    }

    [Fact]
    public async Task Respaldar_ListarYDescargar_DevuelveElMismoContenido_SoloAlOperadorDueno()
    {
        await using var entorno = await EntornoAprovisionamiento.CrearAsync(postgres, azurite: azurite);
        var tenant = $"resp-{Guid.NewGuid():N}"[..14];
        var otro = $"resp-{Guid.NewGuid():N}"[..14];
        await entorno.AprovisionarAsync(tenant);
        await entorno.AprovisionarAsync(otro);

        var carpeta = Directory.CreateTempSubdirectory("respaldos-").FullName;
        try
        {
            var original = Path.Combine(carpeta, "silo.dump");
            var bytes = new byte[300_000];
            Random.Shared.NextBytes(bytes);
            await File.WriteAllBytesAsync(original, bytes);

            var (codigoRespaldo, salidaRespaldo) = await EjecutarAsync(entorno, "respaldar", "--id", tenant, "--archivo", original);
            var (_, listado) = await EjecutarAsync(entorno, "listar-respaldos", "--id", tenant);
            var (_, listadoDelOtro) = await EjecutarAsync(entorno, "listar-respaldos", "--id", otro);
            var descargado = Path.Combine(carpeta, "restaurar.dump");
            var (codigoDescarga, _) = await EjecutarAsync(entorno, "descargar-respaldo", "--id", tenant, "--archivo", descargado);

            Assert.Equal(0, codigoRespaldo);
            Assert.Contains("Respaldo guardado: respaldos/silo-", salidaRespaldo, StringComparison.Ordinal);
            Assert.Contains("Retención (35 días)", salidaRespaldo, StringComparison.Ordinal);
            Assert.Contains("respaldos/silo-", listado, StringComparison.Ordinal);
            Assert.Contains("no tiene respaldos", listadoDelOtro, StringComparison.Ordinal);
            Assert.Equal(0, codigoDescarga);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(descargado));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
