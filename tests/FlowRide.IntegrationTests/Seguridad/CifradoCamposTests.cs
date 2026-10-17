using System.Security.Cryptography;
using FlowRide.Application.Seguridad.Entrada.Puertos;
using FlowRide.Aprovisionamiento;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Infrastructure.Persistencia;
using FlowRide.Infrastructure.Seguridad;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowRide.IntegrationTests.Seguridad;

/// <summary>Cifrado determinista de campos (ADR-0014), sin base de datos.</summary>
[Trait("Category", "Integration")]
public class CifradorCamposTests
{
    private const string ClaveV1 = "clave-maestra-v1-de-pruebas-0123456789";
    private const string ClaveV2 = "clave-maestra-v2-de-pruebas-0123456789";

    private static CifradorCampos Cifrador(string vigente = "v1") =>
        new(new Dictionary<string, string> { ["v1"] = ClaveV1, ["v2"] = ClaveV2 }, vigente);

    [Fact]
    public void Cifrar_EsDeterministaYReversible()
    {
        var cifrador = Cifrador();

        var cifrado = cifrador.Cifrar("+573001234567");

        Assert.StartsWith("v1.", cifrado, StringComparison.Ordinal);
        Assert.DoesNotContain("3001234567", cifrado, StringComparison.Ordinal);
        Assert.Equal(cifrado, cifrador.Cifrar("+573001234567"));
        Assert.NotEqual(cifrado, cifrador.Cifrar("+573001234568"));
        Assert.Equal("+573001234567", cifrador.Descifrar(cifrado));
        Assert.True(cifrador.EstaCifradoConVigente(cifrado));
    }

    [Fact]
    public void Rotacion_DescifraLaVersionGuardadaYCifraConLaVigente()
    {
        var cifradoV1 = Cifrador("v1").Cifrar("1001");
        var conV2 = Cifrador("v2");

        Assert.Equal("1001", conV2.Descifrar(cifradoV1));
        Assert.False(conV2.EstaCifradoConVigente(cifradoV1));
        Assert.StartsWith("v2.", conV2.Cifrar("1001"), StringComparison.Ordinal);
    }

    [Fact]
    public void ValorEnClaro_SeDevuelveTalCual_YUnoAlteradoNoSeDescifra()
    {
        var cifrador = Cifrador();
        var cifrado = cifrador.Cifrar("1001");
        var alterado = cifrado[..^2] + (cifrado[^2] == 'A' ? "B" : "A") + cifrado[^1];

        Assert.Equal("+57300", cifrador.Descifrar("+57300"));
        Assert.ThrowsAny<CryptographicException>(() => cifrador.Descifrar(alterado));
        Assert.Throws<InvalidOperationException>(() => cifrador.Descifrar("v9.AAAA"));
    }

    [Fact]
    public void ClavesInvalidas_SeRechazanAlArrancar()
    {
        Assert.Throws<InvalidOperationException>(() => new CifradorCampos(new Dictionary<string, string> { ["v1"] = "corta" }, "v1"));
        Assert.Throws<InvalidOperationException>(() => new CifradorCampos(new Dictionary<string, string> { ["uno"] = ClaveV1 }, "uno"));
        Assert.Throws<InvalidOperationException>(() => new CifradorCampos(new Dictionary<string, string> { ["v1"] = ClaveV1 }, "v2"));
    }
}

/// <summary>El paso de aprovisionamiento deja cifrados los datos anteriores a la Fase 3 (y los de claves viejas).</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class RecifradoSiloTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private EntornoAprovisionamiento _entorno = null!;
    private readonly string _tenant = $"cif-{Guid.NewGuid():N}"[..14];

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await _entorno.AprovisionarAsync(_tenant);
    }

    public async Task DisposeAsync() => await _entorno.DisposeAsync();

    private async Task<int> AsegurarCifradoAsync() => await _entorno.UsarAsync(async servicios =>
    {
        await using var stack = servicios.GetRequiredService<FabricaStackOperador>().Crear(_tenant);
        await using var alcance = stack.CreateAsyncScope();
        return await alcance.ServiceProvider.GetRequiredService<IAsegurarCifradoPuertoEntrada>().AsegurarAsync(CancellationToken.None);
    });

    private async Task<string> ValorEnLaBaseAsync(string sql)
    {
        var baseDelSilo = new NpgsqlConnectionStringBuilder(_entorno.CadenaAdministracion) { Database = $"flowride_{_tenant.Replace('-', '_')}" };
        await using var fuente = NpgsqlDataSource.Create(baseDelSilo.ConnectionString);
        return (string)(await fuente.CreateCommand(sql).ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task DatosEnClaro_QuedanCifrados_YSeLeenIgual()
    {
        // Datos como los de la Fase 2: escritos sin cifrar.
        var conductor = new Conductor(Guid.NewGuid(), "Luis", "+573109998877", RolesSistema.ConductorId, Ahora, "79123456", "C2",
            new DateOnly(2028, 1, 1));
        await using (var fuente = NpgsqlDataSource.Create(await _entorno.CadenaDelSiloAsync(_tenant)))
        {
            var opciones = new DbContextOptionsBuilder<FlowRideDbContext>();
            FlowRideDbContext.Configurar(opciones, fuente);
            await using var sinCifrar = new FlowRideDbContext(opciones.Options);
            sinCifrar.Conductores.Add(conductor);
            await sinCifrar.SaveChangesAsync();
        }
        Assert.Equal("+573109998877", await ValorEnLaBaseAsync($"SELECT telefono_whats_app FROM ops.usuario WHERE id = '{conductor.Id}'"));

        var reescritos = await AsegurarCifradoAsync();

        Assert.Equal(2, reescritos);
        Assert.StartsWith("v1.", await ValorEnLaBaseAsync($"SELECT telefono_whats_app FROM ops.usuario WHERE id = '{conductor.Id}'"), StringComparison.Ordinal);
        Assert.StartsWith("v1.", await ValorEnLaBaseAsync($"SELECT numero_licencia FROM ops.conductor WHERE id = '{conductor.Id}'"), StringComparison.Ordinal);
        Assert.Equal(0, await AsegurarCifradoAsync());

        await using var silo = await _entorno.AbrirSiloAsync(_tenant);
        var leido = await silo.Contexto.Conductores.SingleAsync(c => c.TelefonoWhatsApp == "+573109998877");
        Assert.Equal("79123456", leido.NumeroLicencia);
    }
}
