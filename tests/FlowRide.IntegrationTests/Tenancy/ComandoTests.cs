using FlowRide.Aprovisionamiento;

namespace FlowRide.IntegrationTests.Tenancy;

/// <summary>Interpretación de los argumentos de la herramienta de aprovisionamiento.</summary>
[Trait("Category", "Integration")]
public class ComandoTests
{
    [Fact]
    public void CrearOperador_ConTodasLasOpciones_SeInterpreta()
    {
        var ok = Comando.TryParse(
            ["crear-operador", "--id", "tenant-c", "--razon-social", "Transportes C", "--nit", "900", "--correo", "c@c.co"],
            out var comando, out _);

        Assert.True(ok);
        Assert.Equal(Comando.CrearOperador, comando.Nombre);
        Assert.Equal("tenant-c", comando.Opcion("id"));
        Assert.Equal("Transportes C", comando.Opcion("razon-social"));
    }

    [Fact]
    public void CrearOperador_ConAdministradorInicial_SeInterpreta()
    {
        var ok = Comando.TryParse(
            ["crear-operador", "--id", "tenant-c", "--razon-social", "C", "--nit", "900", "--correo", "c@c.co",
                "--admin-nombre", "Ana", "--admin-correo", "ana@c.co", "--admin-telefono", "+573001112233"],
            out var comando, out var error);

        Assert.True(ok, error);
        Assert.True(comando.Tiene("admin-correo"));
        Assert.Equal("ana@c.co", comando.Opcion("admin-correo"));
    }

    [Fact]
    public void CrearOperador_ConSoloAlgunasOpcionesDelAdministrador_SeRechaza()
    {
        var ok = Comando.TryParse(
            ["crear-operador", "--id", "tenant-c", "--razon-social", "C", "--nit", "900", "--correo", "c@c.co",
                "--admin-correo", "ana@c.co"],
            out _, out var error);

        Assert.False(ok);
        Assert.Contains("van juntas", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("listar")]
    [InlineData("migrar-todos")]
    [InlineData("ayuda")]
    [InlineData("--help")]
    public void ComandosSinOpciones_SeInterpretan(string nombre) => Assert.True(Comando.TryParse([nombre], out _, out _));

    [Theory]
    [InlineData(new string[0], "Falta el comando")]
    [InlineData(new[] { "borrar-todo" }, "Comando desconocido")]
    [InlineData(new[] { "migrar" }, "Falta la opción --id")]
    [InlineData(new[] { "migrar", "--id" }, "Falta el valor de '--id'")]
    [InlineData(new[] { "migrar", "--id", "--otra" }, "Falta el valor de '--id'")]
    [InlineData(new[] { "migrar", "id", "tenant-a" }, "Se esperaba una opción")]
    [InlineData(new[] { "migrar", "--id", "tenant-a", "--forzar", "si" }, "Opción desconocida")]
    public void ArgumentosInvalidos_DevuelvenUnErrorClaro(string[] args, string mensaje)
    {
        Assert.False(Comando.TryParse(args, out _, out var error));
        Assert.Contains(mensaje, error, StringComparison.Ordinal);
    }
}
