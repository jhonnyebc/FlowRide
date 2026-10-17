using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Aprovisionamiento;
using FlowRide.Domain.Entidades;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;
using Type = System.Type;

namespace FlowRide.ArchitectureTests;

/// <summary>
/// Estilo de puertos y adaptadores acordado con el profesor (proyecto de referencia uco-test-api), adaptado a .NET:
/// adaptador de entrada → puerto de entrada → caso de uso → servicios de dominio y puertos de salida.
/// </summary>
public class EstiloHexagonalTests
{
    private static readonly Assembly DomainAssembly = typeof(Conductor).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ICasoUsoConRetorno<,>).Assembly;
    private static readonly Assembly HerramientaAssembly = typeof(Comandos).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    private static readonly Architecture Arquitectura = new ArchLoader()
        .LoadAssemblies(DomainAssembly, ApplicationAssembly, HerramientaAssembly, ApiAssembly)
        .Build();

    private static IEnumerable<Type> ClasesConcretas(Assembly assembly) =>
        assembly.GetTypes().Where(tipo => tipo is { IsClass: true, IsAbstract: false } && !tipo.IsNested);

    [Fact]
    public void LosCasosDeUso_ImplementanUnaInterfazGenericaDeCasoDeUso()
    {
        var genericas = new[] { typeof(ICasoUsoConRetorno<,>), typeof(ICasoUsoSinRetorno<>) };
        var casosUso = ClasesConcretas(ApplicationAssembly).Where(tipo => tipo.Name.EndsWith("CasoUso", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(casosUso);
        Assert.All(casosUso, casoUso => Assert.True(
            casoUso.GetInterfaces().Any(i => i.IsGenericType && genericas.Contains(i.GetGenericTypeDefinition())),
            $"{casoUso.Name} debe implementar ICasoUsoConRetorno<,> o ICasoUsoSinRetorno<>."));
    }

    [Fact]
    public void LosCasosDeUso_VivenEnUnaCarpetaCasosUso()
    {
        var casosUso = ClasesConcretas(ApplicationAssembly).Where(tipo => tipo.Name.EndsWith("CasoUso", StringComparison.Ordinal));

        Assert.All(casosUso, casoUso => Assert.EndsWith(".CasosUso", casoUso.Namespace, StringComparison.Ordinal));
    }

    [Fact]
    public void LosAdaptadoresDeEntrada_ImplementanSuPuertoDeEntrada()
    {
        var adaptadores = ClasesConcretas(ApplicationAssembly)
            .Where(tipo => tipo.Name.EndsWith("AdaptadorEntrada", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(adaptadores);
        Assert.All(adaptadores, adaptador =>
        {
            var puerto = $"I{adaptador.Name[..^"AdaptadorEntrada".Length]}PuertoEntrada";
            Assert.True(adaptador.GetInterfaces().Any(i => i.Name == puerto), $"{adaptador.Name} debe implementar {puerto}.");
        });
    }

    [Fact]
    public void LosRepositorios_SonPuertosDelDominio()
    {
        // Los servicios de dominio (XxxDominio) los usan, así que viven en Domain como interfaces.
        var repositoriosFueraDelDominio = ApplicationAssembly.GetTypes()
            .Where(tipo => tipo.IsInterface && tipo.Name.EndsWith("Repositorio", StringComparison.Ordinal))
            .Select(tipo => tipo.FullName);

        Assert.Empty(repositoriosFueraDelDominio);
        Assert.Contains(DomainAssembly.GetTypes(), tipo => tipo.IsInterface && tipo.Name == "IOperadorRepositorio");
    }

    [Fact]
    public void LaHerramienta_SoloConocePuertosDeEntrada() =>
        // Como el controlador del proyecto de referencia: depende del puerto de entrada, nunca del caso de uso
        // ni del dominio directamente.
        Types().That().ResideInAssembly(HerramientaAssembly)
            .Should().NotDependOnAny(Types().That().ResideInNamespace("FlowRide.Application.Tenancy.CasosUso"))
            .AndShould().NotDependOnAny(Types().That().ResideInAssembly(DomainAssembly))
            .Check(Arquitectura);

    [Fact]
    public void LosEndpointsDeLaApi_SoloConocenPuertosDeEntrada() =>
        // Los adaptadores de entrada HTTP de cada módulo funcional, igual que la herramienta: puerto de entrada y DTO.
        Types().That().ResideInNamespace("FlowRide.API.Identidad").Or().ResideInNamespace("FlowRide.API.Auditoria")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^FlowRide\.Application\.\w+\.CasosUso$"))
            .AndShould().NotDependOnAny(Types().That().ResideInAssembly(DomainAssembly))
            .Check(Arquitectura);
}
