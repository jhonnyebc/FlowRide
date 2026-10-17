using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Tenancy;

public class AprovisionarOperadorCasoUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly AprovisionarOperadorDominio Datos = new("tenant-a", "Transportes A", "900123456-1", "ops@a.co");

    private readonly OperadoresEnMemoria _operadores = new();
    private readonly ServidorEnMemoria _servidor = new();
    private readonly MigradorEnMemoria _migrador = new();
    private readonly SecretosEnMemoria _secretos = new();

    private AprovisionarOperadorCasoUso CasoUso() =>
        new(new OperadorDominio(_operadores), _operadores, _servidor, _migrador, _secretos, new GeneradorSecuencial(), new RelojFijo(Ahora));

    [Fact]
    public async Task OperadorNuevo_QuedaActivoConSiloSecretoYEsquema()
    {
        var resultado = await CasoUso().EjecutarAsync(Datos, CancellationToken.None);

        Assert.True(resultado.OperadorNuevo);
        Assert.True(resultado.ContrasenaNueva);
        Assert.Equal(["20261017_Inicial"], resultado.MigracionesAplicadas);
        Assert.Equal(1, _operadores.Creados);
        Assert.Equal(EstadoOperador.Activo, _operadores.Operadores["tenant-a"].Estado);
        Assert.Equal(Ahora, _operadores.Operadores["tenant-a"].CreadoEn);
        Assert.Equal("clave-1", _servidor.Silos["flowride_tenant_a"]);
        Assert.Equal("clave-1", _secretos.Valores[("flowride-tenant-a", ClavesSecretos.ContrasenaSilo)]);
        Assert.Equal("Database=flowride_tenant_a;Password=clave-1",
            _secretos.Valores[("flowride-tenant-a", ClavesSecretos.CadenaConexionSilo)]);
    }

    [Fact]
    public async Task SegundaEjecucion_EsIdempotente()
    {
        var casoUso = CasoUso();
        await casoUso.EjecutarAsync(Datos, CancellationToken.None);

        var resultado = await casoUso.EjecutarAsync(Datos with { RazonSocial = "Transportes A SAS" }, CancellationToken.None);

        Assert.False(resultado.OperadorNuevo);
        Assert.False(resultado.ContrasenaNueva);
        Assert.Empty(resultado.MigracionesAplicadas);
        Assert.Equal(1, _operadores.Creados);
        Assert.Equal("Transportes A SAS", _operadores.Operadores["tenant-a"].RazonSocial);
        Assert.Equal("clave-1", _servidor.Silos["flowride_tenant_a"]);
    }

    [Fact]
    public async Task ContrasenaExistente_SeReutilizaYElRolLaToma()
    {
        // Caso de un Silo creado antes de la herramienta: el secreto manda y el rol se ajusta a él.
        _secretos.Valores[("flowride-tenant-a", ClavesSecretos.ContrasenaSilo)] = "clave-previa";

        var resultado = await CasoUso().EjecutarAsync(Datos, CancellationToken.None);

        Assert.False(resultado.ContrasenaNueva);
        Assert.Equal("clave-previa", _servidor.Silos["flowride_tenant_a"]);
    }

    [Fact]
    public async Task OperadoresDistintos_NoCompartenCredenciales()
    {
        var casoUso = CasoUso();

        await casoUso.EjecutarAsync(Datos, CancellationToken.None);
        await casoUso.EjecutarAsync(new AprovisionarOperadorDominio("tenant-b", "Transportes B", "900", "b@b.co"), CancellationToken.None);

        Assert.NotEqual(_servidor.Silos["flowride_tenant_a"], _servidor.Silos["flowride_tenant_b"]);
    }

    [Fact]
    public async Task FalloAlMigrar_DejaElOperadorRegistradoSinActivarYSePuedeReintentar()
    {
        _migrador.FallaPara = "flowride_tenant_a";

        await Assert.ThrowsAsync<InvalidOperationException>(() => CasoUso().EjecutarAsync(Datos, CancellationToken.None));
        Assert.Equal(EstadoOperador.Aprovisionando, _operadores.Operadores["tenant-a"].Estado);

        _migrador.FallaPara = null;
        var resultado = await CasoUso().EjecutarAsync(Datos, CancellationToken.None);

        Assert.False(resultado.ContrasenaNueva);
        Assert.Equal(EstadoOperador.Activo, resultado.Operador.Estado);
    }

    [Fact]
    public async Task IdInvalido_NoCreaNada()
    {
        var error = await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            CasoUso().EjecutarAsync(Datos with { TenantId = "Tenant A" }, CancellationToken.None));

        Assert.Contains("Tenant A", error.Message, StringComparison.Ordinal);
        Assert.Empty(_operadores.Operadores);
        Assert.Empty(_servidor.Silos);
        Assert.Empty(_secretos.Valores);
    }

    [Fact]
    public async Task NitDeOtroOperador_SeRechazaSinCrearRecursos()
    {
        _operadores.Registrar("tenant-x", nit: Datos.Nit);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => CasoUso().EjecutarAsync(Datos, CancellationToken.None));

        Assert.False(_operadores.Operadores.ContainsKey("tenant-a"));
        Assert.Empty(_servidor.Silos);
    }

    [Fact]
    public async Task OperadorSuspendido_NoSeReaprovisiona()
    {
        _operadores.Registrar("tenant-a", EstadoOperador.Suspendido, nit: Datos.Nit);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => CasoUso().EjecutarAsync(Datos, CancellationToken.None));

        Assert.Empty(_servidor.Silos);
        Assert.Equal(EstadoOperador.Suspendido, _operadores.Operadores["tenant-a"].Estado);
    }

    [Fact]
    public async Task DatosNulos_SeRechazan() =>
        await Assert.ThrowsAsync<ArgumentNullException>(() => CasoUso().EjecutarAsync(null!, CancellationToken.None));
}
