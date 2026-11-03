using FlowRide.Domain.Entidades;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlowRide.IntegrationTests.Tenancy;

/// <summary>
/// Spike 3 (ADR-0002): dos operadores aprovisionados en el mismo servidor no comparten persistencia ni credenciales.
/// Criterio de éxito: 0 lecturas o escrituras entre tenants.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class AislamientoSiloTests(PostgresFixture postgres) : IAsyncLifetime
{
    private EntornoAprovisionamiento _entorno = null!;

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await _entorno.AprovisionarAsync("aislado-a");
        await _entorno.AprovisionarAsync("aislado-b");
    }

    public async Task DisposeAsync() => await _entorno.DisposeAsync();

    [Fact]
    public async Task CredencialesDeUnOperador_NoAbrenLaBaseDeOtro()
    {
        var credencialesDeA = new NpgsqlConnectionStringBuilder(await _entorno.CadenaDelSiloAsync("aislado-a"))
        {
            Database = "flowride_aislado_b"
        };

        await using var conexion = new NpgsqlConnection(credencialesDeA.ConnectionString);
        var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.OpenAsync());

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task CadaOperadorTieneCredencialesDistintas()
    {
        var cadenaA = new NpgsqlConnectionStringBuilder(await _entorno.CadenaDelSiloAsync("aislado-a"));
        var cadenaB = new NpgsqlConnectionStringBuilder(await _entorno.CadenaDelSiloAsync("aislado-b"));

        Assert.NotEqual(cadenaA.Username, cadenaB.Username);
        Assert.NotEqual(cadenaA.Password, cadenaB.Password);
        Assert.NotEqual(cadenaA.Database, cadenaB.Database);
    }

    [Fact]
    public async Task LosDatosDeUnOperador_NoSeVenDesdeOtro()
    {
        var rolDeA = new Rol(Guid.NewGuid(), "Supervisor", "Rol del operador A");
        await using (var siloA = await _entorno.AbrirSiloAsync("aislado-a"))
        {
            siloA.Contexto.Roles.Add(rolDeA);
            await siloA.Contexto.SaveChangesAsync();
        }

        await using var siloB = await _entorno.AbrirSiloAsync("aislado-b");
        Assert.False(await siloB.Contexto.Roles.AnyAsync(rol => rol.Id == rolDeA.Id));

        // El mismo nombre (único dentro de un Silo) no choca: cada operador tiene su propio catálogo de roles.
        var rolDeB = new Rol(Guid.NewGuid(), "Supervisor", "Rol del operador B");
        siloB.Contexto.Roles.Add(rolDeB);
        await siloB.Contexto.SaveChangesAsync();

        await using var siloAOtraVez = await _entorno.AbrirSiloAsync("aislado-a");
        Assert.True(await siloAOtraVez.Contexto.Roles.AnyAsync(rol => rol.Id == rolDeA.Id));
        Assert.False(await siloAOtraVez.Contexto.Roles.AnyAsync(rol => rol.Id == rolDeB.Id));
    }
}
