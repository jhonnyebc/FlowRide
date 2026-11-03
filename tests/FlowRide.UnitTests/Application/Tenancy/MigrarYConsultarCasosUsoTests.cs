using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Tenancy;

public class MigrarSilosCasoUsoTests
{
    private readonly OperadoresEnMemoria _operadores = new();
    private readonly MigradorEnMemoria _migrador = new();
    private readonly SecretosEnMemoria _secretos = new();

    private MigrarSilosCasoUso CasoUso() => new(new OperadorDominio(_operadores), _operadores, _migrador, _secretos);

    private void Registrar(string id, EstadoOperador estado = EstadoOperador.Activo, bool conSecreto = true)
    {
        var operador = _operadores.Registrar(id, estado);
        if (conSecreto) _secretos.CadenaPara(operador);
    }

    [Fact]
    public async Task UnOperador_AplicaSusPendientes()
    {
        Registrar("tenant-a");

        var resultado = Assert.Single(await CasoUso().EjecutarAsync(new MigrarSilosDominio("tenant-a"), CancellationToken.None));

        Assert.True(resultado.Exitoso);
        Assert.Equal(["20261017_Inicial"], resultado.MigracionesAplicadas);
    }

    [Fact]
    public async Task UnOperadorNoRegistrado_Falla() =>
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new MigrarSilosDominio("tenant-x"), CancellationToken.None));

    [Fact]
    public async Task UnOperadorSinSecretoDeConexion_FallaCerrado()
    {
        Registrar("tenant-a", conSecreto: false);

        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(new MigrarSilosDominio("tenant-a"), CancellationToken.None));

        Assert.Contains(ClavesSecretos.CadenaConexionSilo, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Todos_OmiteSuspendidosVaEnOrdenYSigueTrasUnFallo()
    {
        Registrar("tenant-c");
        Registrar("tenant-a");
        Registrar("tenant-b", conSecreto: false);
        Registrar("tenant-s", EstadoOperador.Suspendido);

        var resultados = await CasoUso().EjecutarAsync(MigrarSilosDominio.Todos, CancellationToken.None);

        Assert.Equal(["tenant-a", "tenant-b", "tenant-c"], resultados.Select(resultado => resultado.TenantId));
        Assert.True(resultados[0].Exitoso);
        Assert.False(resultados[1].Exitoso);
        Assert.NotNull(resultados[1].Error);
        Assert.True(resultados[2].Exitoso);
    }

    [Fact]
    public async Task Todos_SegundaVez_NoHayPendientes()
    {
        Registrar("tenant-a");
        await CasoUso().EjecutarAsync(MigrarSilosDominio.Todos, CancellationToken.None);

        var resultados = await CasoUso().EjecutarAsync(MigrarSilosDominio.Todos, CancellationToken.None);

        Assert.Empty(Assert.Single(resultados).MigracionesAplicadas);
    }

    [Fact]
    public async Task DatosNulos_SeRechazan() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CasoUso().EjecutarAsync(null!, CancellationToken.None));
}

public class ConsultarOperadoresCasoUsoTests
{
    private readonly OperadoresEnMemoria _operadores = new();

    [Fact]
    public async Task SinEstado_DevuelveTodosEnOrdenPorId()
    {
        _operadores.Registrar("tenant-b");
        _operadores.Registrar("tenant-a", EstadoOperador.Suspendido);

        var operadores = await new ConsultarOperadoresCasoUso(_operadores).EjecutarAsync(null, CancellationToken.None);

        Assert.Equal(["tenant-a", "tenant-b"], operadores.Select(operador => operador.Id));
    }

    [Fact]
    public async Task ConEstado_FiltraPorEl()
    {
        _operadores.Registrar("tenant-b");
        _operadores.Registrar("tenant-a", EstadoOperador.Suspendido);

        var operadores = await new ConsultarOperadoresCasoUso(_operadores).EjecutarAsync(EstadoOperador.Suspendido, CancellationToken.None);

        Assert.Equal("tenant-a", Assert.Single(operadores).Id);
    }
}
