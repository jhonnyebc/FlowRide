using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowRide.IntegrationTests.Tenancy;

/// <summary>Pipeline de aprovisionamiento de un operador contra un PostgreSQL real (RF-03, ADR-0010).</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class AprovisionamientoTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CrearOperador_DejaSiloMigradoSecretoYOperadorActivo()
    {
        await using var entorno = await EntornoAprovisionamiento.CrearAsync(postgres);

        var resultado = await entorno.AprovisionarAsync("prov-nuevo");

        Assert.True(resultado.OperadorNuevo);
        Assert.NotEmpty(resultado.MigracionesAplicadas);
        var operador = await entorno.UsarAsync(servicios =>
            servicios.GetRequiredService<IOperadorRepositorio>().ConsultarPorIdAsync("prov-nuevo", CancellationToken.None));
        Assert.Equal(EstadoOperador.Activo, operador!.Estado);
        Assert.True(File.Exists(Path.Combine(entorno.RaizSecretos, "flowride-prov-nuevo", "secrets.json")));

        // Con sus propias credenciales, el operador ve su esquema con la versión registrada.
        await using var silo = await entorno.AbrirSiloAsync("prov-nuevo");
        Assert.Empty(await silo.Contexto.Database.GetPendingMigrationsAsync());
        Assert.Equal(resultado.MigracionesAplicadas, await silo.Contexto.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Reaprovisionar_EsIdempotente()
    {
        await using var entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await entorno.AprovisionarAsync("prov-repetido");
        var cadenaInicial = await entorno.CadenaDelSiloAsync("prov-repetido");

        var resultado = await entorno.AprovisionarAsync("prov-repetido");

        Assert.False(resultado.OperadorNuevo);
        Assert.False(resultado.ContrasenaNueva);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Equal(cadenaInicial, await entorno.CadenaDelSiloAsync("prov-repetido"));
        await using var silo = await entorno.AbrirSiloAsync("prov-repetido");
        Assert.True(await silo.Contexto.Database.CanConnectAsync());
    }

    [Fact]
    public async Task SiloCreadoAntesDeLaHerramienta_ElRolTomaLaContrasenaDelSecreto()
    {
        await using var entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await EntornoAprovisionamiento.EjecutarComoAdministradorAsync(entorno.CadenaAdministracion,
            "CREATE ROLE flowride_prov_previo LOGIN PASSWORD 'contrasena-vieja'");
        await EntornoAprovisionamiento.EjecutarComoAdministradorAsync(entorno.CadenaAdministracion,
            "CREATE DATABASE flowride_prov_previo OWNER flowride_prov_previo");

        var resultado = await entorno.AprovisionarAsync("prov-previo");

        Assert.True(resultado.ContrasenaNueva);
        await using var silo = await entorno.AbrirSiloAsync("prov-previo");
        Assert.True(await silo.Contexto.Database.CanConnectAsync());

        var conContrasenaVieja = new NpgsqlConnectionStringBuilder(await entorno.CadenaDelSiloAsync("prov-previo"))
        {
            Password = "contrasena-vieja"
        };
        await using var conexion = new NpgsqlConnection(conContrasenaVieja.ConnectionString);
        var error = await Assert.ThrowsAsync<PostgresException>(() => conexion.OpenAsync());
        Assert.Equal(PostgresErrorCodes.InvalidPassword, error.SqlState);
    }

    [Fact]
    public async Task MigrarTodos_ConSilosAlDia_NoAplicaNada()
    {
        await using var entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await entorno.AprovisionarAsync("prov-uno");
        await entorno.AprovisionarAsync("prov-dos");

        var resultados = await entorno.UsarAsync(servicios =>
            servicios.GetRequiredService<IMigrarSilosPuertoEntrada>().MigrarAsync(new MigrarSilosPeticion(), CancellationToken.None));

        Assert.Equal(["prov-dos", "prov-uno"], resultados.Silos.Select(resultado => resultado.TenantId));
        Assert.All(resultados.Silos, resultado =>
        {
            Assert.True(resultado.Exitoso);
            Assert.Empty(resultado.MigracionesAplicadas);
        });
    }
}
