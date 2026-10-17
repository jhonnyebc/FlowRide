using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FlowRide.Domain.Entidades;
using FlowRide.Infrastructure;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace FlowRide.ArchitectureTests;

/// <summary>Las dependencias entre capas apuntan hacia el dominio (ADR-0001, ADR-0017).</summary>
public class CapasTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Conductor).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        System.Reflection.Assembly.Load("FlowRide.Application");
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly Architecture Arquitectura = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly)
        .Build();

    private static readonly IObjectProvider<IType> Domain =
        Types().That().ResideInAssembly(DomainAssembly).As("Domain");
    private static readonly IObjectProvider<IType> Application =
        Types().That().ResideInAssembly(ApplicationAssembly).As("Application");
    private static readonly IObjectProvider<IType> Infrastructure =
        Types().That().ResideInAssembly(InfrastructureAssembly).As("Infrastructure");
    private static readonly IObjectProvider<IType> Api =
        Types().That().ResideInAssembly(ApiAssembly).As("API");

    [Fact]
    public void Domain_NoDependeDeOtrasCapas() =>
        Types().That().Are(Domain)
            .Should().NotDependOnAny(Application).AndShould().NotDependOnAny(Infrastructure).AndShould().NotDependOnAny(Api)
            .Check(Arquitectura);

    [Fact]
    public void Application_SoloDependeDeDomain() =>
        Types().That().Are(Application)
            .Should().NotDependOnAny(Infrastructure).AndShould().NotDependOnAny(Api)
            .Check(Arquitectura);

    [Fact]
    public void Infrastructure_NoDependeDeLaApi() =>
        Types().That().Are(Infrastructure).Should().NotDependOnAny(Api).Check(Arquitectura);

    [Fact]
    public void LaApi_NoUsaElCatalogoNiElAprovisionamiento() =>
        // El stack de un operador solo conoce su propio Silo; el catálogo de plataforma y el aprovisionamiento
        // son de la herramienta de plataforma (ADR-0002, ADR-0010).
        // Límite conocido: ArchUnitNET no analiza el cuerpo del Main de nivel superior (Program.cs), solo las clases.
        Types().That().Are(Api)
            .Should().NotDependOnAny(
                typeof(FlowRide.Infrastructure.Tenancy.CatalogoDbContext),
                typeof(FlowRide.Infrastructure.Tenancy.OperadorRepositorioAdaptador),
                typeof(FlowRide.Infrastructure.Tenancy.ServidorSilosPostgres),
                typeof(FlowRide.Application.Tenancy.CasosUso.AprovisionarOperadorCasoUso),
                typeof(FlowRide.Application.Tenancy.CasosUso.MigrarSilosCasoUso),
                typeof(FlowRide.Application.Tenancy.Entrada.Puertos.IAprovisionarOperadorPuertoEntrada))
            .Check(Arquitectura);

    [Fact]
    public void Domain_SoloReferenciaElRuntimeDeNet()
    {
        // "Domain no depende de nada externo": ni EF Core, ni ASP.NET, ni Npgsql, ni paquetes NuGet.
        var externas = DomainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                           && name is not "netstandard" and not "mscorlib")
            .ToArray();

        Assert.Empty(externas);
    }
}
