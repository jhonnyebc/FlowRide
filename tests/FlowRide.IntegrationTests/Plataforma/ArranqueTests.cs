using FlowRide.IntegrationTests.Infraestructura;

namespace FlowRide.IntegrationTests.Plataforma;

/// <summary>El stack no arranca con configuración incompleta: nunca usa valores por defecto (ADR-0014).</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class ArranqueTests(PostgresFixture postgres)
{
    [Fact]
    public async Task SinCadenaDeConexionDelSilo_ElArranqueFalla()
    {
        await using var api = new FlowRideApiFactory(cadenaConexion: null);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("ConnectionStrings:Silo", ExcepcionMasInterna(error).Message);
    }

    [Fact]
    public async Task SinTenantId_ElArranqueFalla()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion, tenantId: null);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("FlowRide:TenantId", ExcepcionMasInterna(error).Message);
    }

    [Fact]
    public async Task SinSecretoDelClienteDeIdentidad_ElArranqueFalla()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion) { SecretoCliente = null };

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("FlowRide:Identidad:SecretoCliente", ExcepcionMasInterna(error).Message);
    }

    [Fact]
    public async Task SinUrlDeKeycloak_ElArranqueFalla()
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion) { UrlKeycloak = null };

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("FlowRide:Identidad:UrlKeycloak", ExcepcionMasInterna(error).Message);
    }

    [Theory]
    [InlineData("Tenant A")]
    [InlineData("TENANT-A")]
    [InlineData("tenant_a")]
    public async Task ConTenantIdInvalido_ElArranqueFalla(string tenantId)
    {
        await using var api = new FlowRideApiFactory(postgres.CadenaConexion, tenantId);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("FlowRide:TenantId", ExcepcionMasInterna(error).Message);
    }

    private static Exception ExcepcionMasInterna(Exception error)
    {
        while (error.InnerException is not null) error = error.InnerException;
        return error;
    }
}
