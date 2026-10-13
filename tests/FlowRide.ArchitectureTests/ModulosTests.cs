using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using FlowRide.Domain.Entidades;
using FlowRide.Infrastructure;

namespace FlowRide.ArchitectureTests;

/// <summary>
/// Cada módulo es el primer namespace después de la capa (por ejemplo FlowRide.Domain.Assignment o
/// FlowRide.API.Plataforma) y no puede haber ciclos entre módulos de una misma capa (ADR-0001).
/// Las dependencias entre sub-namespaces de un mismo módulo están permitidas.
/// </summary>
public class ModulosTests
{
    private static readonly Architecture Arquitectura = new ArchLoader()
        .LoadAssemblies(
            typeof(Conductor).Assembly,
            System.Reflection.Assembly.Load("FlowRide.Application"),
            typeof(DependencyInjection).Assembly,
            typeof(Program).Assembly)
        .Build();

    [Theory]
    [InlineData("FlowRide.Domain")]
    [InlineData("FlowRide.Application")]
    [InlineData("FlowRide.Infrastructure")]
    [InlineData("FlowRide.API")]
    public void LosModulosDeCadaCapaNoTienenCiclos(string capa)
    {
        var grafo = ConstruirGrafoDeModulos(capa);

        var ciclo = BuscarCiclo(grafo);

        Assert.True(ciclo is null, $"Ciclo entre módulos de {capa}: {string.Join(" -> ", ciclo ?? [])}");
    }

    private static Dictionary<string, SortedSet<string>> ConstruirGrafoDeModulos(string capa)
    {
        var grafo = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var tipo in Arquitectura.Types)
        {
            var origen = ModuloDe(tipo, capa);
            if (origen is null) continue;

            if (!grafo.TryGetValue(origen, out var destinos))
                grafo[origen] = destinos = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var dependencia in tipo.Dependencies)
            {
                var destino = ModuloDe(dependencia.Target, capa);
                if (destino is not null && destino != origen)
                    destinos.Add(destino);
            }
        }

        return grafo;
    }

    private static string? ModuloDe(IType tipo, string capa)
    {
        var espacio = tipo.Namespace.FullName;
        if (!espacio.StartsWith(capa + ".", StringComparison.Ordinal)) return null;

        var resto = espacio[(capa.Length + 1)..];
        var punto = resto.IndexOf('.');
        return punto < 0 ? resto : resto[..punto];
    }

    [Fact]
    public void BuscarCiclo_DetectaUnCicloIndirecto()
    {
        var grafo = new Dictionary<string, SortedSet<string>>
        {
            ["Asignacion"] = ["Solicitudes"],
            ["Solicitudes"] = ["Viajes"],
            ["Viajes"] = ["Asignacion"],
            ["Tenancy"] = []
        };

        Assert.Equal(["Asignacion", "Solicitudes", "Viajes", "Asignacion"], BuscarCiclo(grafo));
        Assert.Null(BuscarCiclo(new Dictionary<string, SortedSet<string>> { ["A"] = ["B"], ["B"] = [] }));
    }

    private static List<string>? BuscarCiclo(Dictionary<string, SortedSet<string>> grafo)
    {
        var visitados = new HashSet<string>(StringComparer.Ordinal);
        var enCurso = new List<string>();

        List<string>? Visitar(string modulo)
        {
            var posicion = enCurso.IndexOf(modulo);
            if (posicion >= 0) return [.. enCurso[posicion..], modulo];
            if (!visitados.Add(modulo)) return null;

            enCurso.Add(modulo);
            foreach (var siguiente in grafo.GetValueOrDefault(modulo) ?? [])
            {
                if (Visitar(siguiente) is { } ciclo) return ciclo;
            }
            enCurso.RemoveAt(enCurso.Count - 1);
            return null;
        }

        foreach (var modulo in grafo.Keys.Order(StringComparer.Ordinal))
        {
            if (Visitar(modulo) is { } ciclo) return ciclo;
        }

        return null;
    }
}
