using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Tenancy;

/// <summary>Los adaptadores de entrada validan la petición, la convierten en datos del dominio y arman la respuesta.</summary>
public class AdaptadoresEntradaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 8, 0, 0, TimeSpan.Zero);

    private sealed class CasoUsoGrabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder)
        : ICasoUsoConRetorno<TEntrada, TSalida>
    {
        public List<TEntrada> Recibidos { get; } = [];

        public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
        {
            Recibidos.Add(datos);
            return Task.FromResult(responder(datos));
        }
    }

    private static Operador OperadorActivo(string id)
    {
        var operador = new Operador(id, "Transportes A", "900", "ops@a.co", Ahora);
        operador.Activar();
        return operador;
    }

    [Fact]
    public async Task Aprovisionar_RecortaYConvierteLaPeticionYArmaLaRespuesta()
    {
        var casoUso = new CasoUsoGrabador<AprovisionarOperadorDominio, ResultadoAprovisionamiento>(datos =>
            new ResultadoAprovisionamiento(OperadorActivo(datos.TenantId), true, true, ["Inicial"]));
        var adaptador = new AprovisionarOperadorAdaptadorEntrada(casoUso);

        var respuesta = await adaptador.AprovisionarAsync(new AprovisionarOperadorPeticion
        {
            TenantId = " tenant-a ",
            RazonSocial = " Transportes A ",
            Nit = " 900 ",
            CorreoContacto = " ops@a.co "
        }, CancellationToken.None);

        Assert.Equal(new AprovisionarOperadorDominio("tenant-a", "Transportes A", "900", "ops@a.co"), Assert.Single(casoUso.Recibidos));
        Assert.Equal("tenant-a", respuesta.TenantId);
        Assert.Equal("Activo", respuesta.Estado);
        Assert.Equal("flowride_tenant_a", respuesta.NombreSilo);
        Assert.Equal("flowride-tenant-a", respuesta.AmbitoSecretos);
        Assert.Equal(["Inicial"], respuesta.MigracionesAplicadas);
        Assert.Equal("Operador aprovisionado correctamente.", respuesta.Mensaje);
    }

    [Fact]
    public async Task Aprovisionar_OperadorExistente_CambiaElMensaje()
    {
        var casoUso = new CasoUsoGrabador<AprovisionarOperadorDominio, ResultadoAprovisionamiento>(datos =>
            new ResultadoAprovisionamiento(OperadorActivo(datos.TenantId), false, false, []));

        var respuesta = await new AprovisionarOperadorAdaptadorEntrada(casoUso).AprovisionarAsync(new AprovisionarOperadorPeticion
        {
            TenantId = "tenant-a",
            RazonSocial = "A",
            Nit = "900",
            CorreoContacto = "a@a.co"
        }, CancellationToken.None);

        Assert.False(respuesta.OperadorNuevo);
        Assert.Contains("ya estaba aprovisionado", respuesta.Mensaje, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "A", "900", "a@a.co")]
    [InlineData("tenant-a", " ", "900", "a@a.co")]
    [InlineData("tenant-a", "A", "", "a@a.co")]
    [InlineData("tenant-a", "A", "900", null)]
    public async Task Aprovisionar_CamposObligatoriosFaltantes_NoLlamaAlCasoDeUso(
        string? tenantId, string? razonSocial, string? nit, string? correo)
    {
        var casoUso = new CasoUsoGrabador<AprovisionarOperadorDominio, ResultadoAprovisionamiento>(_ => throw new InvalidOperationException());

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new AprovisionarOperadorAdaptadorEntrada(casoUso).AprovisionarAsync(
            new AprovisionarOperadorPeticion { TenantId = tenantId, RazonSocial = razonSocial, Nit = nit, CorreoContacto = correo },
            CancellationToken.None));

        Assert.Empty(casoUso.Recibidos);
    }

    [Fact]
    public async Task Aprovisionar_PeticionNula_SeRechaza() =>
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new AprovisionarOperadorAdaptadorEntrada(
                new CasoUsoGrabador<AprovisionarOperadorDominio, ResultadoAprovisionamiento>(_ => throw new InvalidOperationException()))
                .AprovisionarAsync(null!, CancellationToken.None));

    [Theory]
    [InlineData(null, true)]
    [InlineData("  ", true)]
    [InlineData(" tenant-a ", false)]
    public async Task Migrar_SinTenantEsTodos_ConTenantEsUno(string? tenantId, bool esTodos)
    {
        var casoUso = new CasoUsoGrabador<MigrarSilosDominio, IReadOnlyList<ResultadoMigracion>>(_ =>
            [new ResultadoMigracion("tenant-a", ["Inicial"], null), new ResultadoMigracion("tenant-b", [], "sin secreto")]);

        var respuesta = await new MigrarSilosAdaptadorEntrada(casoUso)
            .MigrarAsync(new MigrarSilosPeticion { TenantId = tenantId }, CancellationToken.None);

        var datos = Assert.Single(casoUso.Recibidos);
        Assert.Equal(esTodos, datos.EsTodos);
        if (!esTodos) Assert.Equal("tenant-a", datos.TenantId);
        Assert.Equal(2, respuesta.Silos.Count);
        Assert.False(respuesta.TodosExitosos);
        Assert.False(respuesta.Silos[1].Exitoso);
    }

    [Fact]
    public async Task Migrar_PeticionNula_SeRechaza() =>
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new MigrarSilosAdaptadorEntrada(
                new CasoUsoGrabador<MigrarSilosDominio, IReadOnlyList<ResultadoMigracion>>(_ => []))
                .MigrarAsync(null!, CancellationToken.None));

    [Fact]
    public async Task Consultar_ConvierteOperadoresEnRespuestas()
    {
        var casoUso = new CasoUsoGrabador<EstadoOperador?, IReadOnlyList<Operador>>(_ => [OperadorActivo("tenant-a")]);

        var respuesta = Assert.Single(await new ConsultarOperadoresAdaptadorEntrada(casoUso).ConsultarAsync(CancellationToken.None));

        Assert.Null(Assert.Single(casoUso.Recibidos));
        Assert.Equal(new OperadorRespuesta("tenant-a", "Transportes A", "900", "ops@a.co", "Activo"), respuesta);
    }
}
