using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Domain.Parametros;

public class CatalogoParametrosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ElCatalogo_TieneLosValoresPorDefectoAcordados_YNoIncluyeEta()
    {
        Assert.Equal("60", CatalogoParametros.Buscar(CatalogoParametros.AnticipacionRecordatorioMinutos).ValorPorDefecto);
        Assert.Equal("30", CatalogoParametros.Buscar(CatalogoParametros.VentanaCancelacionMinutos).ValorPorDefecto);
        Assert.Equal("[]", CatalogoParametros.Buscar(CatalogoParametros.FranjasCriticas).ValorPorDefecto);
        Assert.Equal("\"PasajeroYOperadorLogistico\"",
            CatalogoParametros.Buscar(CatalogoParametros.VisibilidadSolicitudesPersonales).ValorPorDefecto);
        Assert.DoesNotContain(CatalogoParametros.Todas, definicion => definicion.Clave.Contains("eta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParametroDesconocido_SeRechaza() =>
        Assert.Throws<FlowRideExcepcion>(() => CatalogoParametros.Buscar("motor.peso_x"));

    [Theory]
    [InlineData("45", "45")]
    [InlineData(" 5 ", "5")]
    [InlineData("1440", "1440")]
    public void Entero_DentroDelRango_SeNormaliza(string valor, string esperado) =>
        Assert.Equal(esperado, CatalogoParametros.Buscar(CatalogoParametros.AnticipacionRecordatorioMinutos).Normalizar(valor));

    [Theory]
    [InlineData("4")]
    [InlineData("1441")]
    [InlineData("1.5")]
    [InlineData("\"60\"")]
    [InlineData("no-es-json")]
    [InlineData(null)]
    public void Entero_FueraDeRangoOMalFormado_SeRechaza(string? valor) =>
        Assert.Throws<FlowRideExcepcion>(() =>
            CatalogoParametros.Buscar(CatalogoParametros.AnticipacionRecordatorioMinutos).Normalizar(valor));

    [Fact]
    public void Opcion_IgnoraMayusculas_YRechazaLasDemas()
    {
        var definicion = CatalogoParametros.Buscar(CatalogoParametros.VisibilidadSolicitudesPersonales);

        Assert.Equal("\"SoloPasajero\"", definicion.Normalizar("\"solopasajero\""));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("\"Todos\""));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("3"));
    }

    [Fact]
    public void Franjas_SeOrdenanSinRepetidos_YSeValidaElFormato()
    {
        var definicion = CatalogoParametros.Buscar(CatalogoParametros.FranjasCriticas);

        Assert.Equal("[\"05:00-07:00\",\"17:00-19:00\"]",
            definicion.Normalizar("[\"17:00-19:00\", \" 05:00-07:00 \", \"05:00-07:00\"]"));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("[\"19:00-17:00\"]"));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("[\"5-7\"]"));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("[\"25:00-26:00\"]"));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("[7]"));
        Assert.Throws<FlowRideExcepcion>(() => definicion.Normalizar("\"05:00-07:00\""));
    }

    [Fact]
    public void Parametro_CambiarSubeLaVersionSoloSiCambiaElValor()
    {
        var definicion = CatalogoParametros.Buscar(CatalogoParametros.VentanaCancelacionMinutos);
        var actor = Guid.NewGuid();
        var parametro = new Parametro(definicion, "45", actor, Ahora);

        Assert.Equal(1, parametro.Version);
        Assert.Equal(TipoParametro.Entero, parametro.Tipo);
        Assert.False(parametro.Cambiar(definicion, " 45 ", null, Ahora.AddMinutes(1)));
        Assert.Equal(actor, parametro.ModificadoPorId);
        Assert.True(parametro.Cambiar(definicion, "15", null, Ahora.AddMinutes(2)));
        Assert.Equal(2, parametro.Version);
        Assert.Equal("15", parametro.Valor);
        Assert.Null(parametro.ModificadoPorId);
        Assert.Equal(Ahora.AddMinutes(2), parametro.ModificadoEn);
    }

    [Fact]
    public void ParametroVigente_SinFilaRigeElValorPorDefecto()
    {
        var definicion = CatalogoParametros.Buscar(CatalogoParametros.VentanaCancelacionMinutos);

        var porDefecto = new ParametroVigente(definicion, null);
        var guardado = new ParametroVigente(definicion, new Parametro(definicion, "10", null, Ahora));

        Assert.True(porDefecto.EsPorDefecto);
        Assert.Equal(("30", 0), (porDefecto.Valor, porDefecto.Version));
        Assert.False(guardado.EsPorDefecto);
        Assert.Equal(("10", 1), (guardado.Valor, guardado.Version));
    }
}

public class PaginacionTests
{
    [Theory]
    [InlineData(null, null, 1, 50)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(3, 500, 3, 200)]
    [InlineData(2, 20, 2, 20)]
    public void SeNormaliza(int? pagina, int? tamano, int paginaEsperada, int tamanoEsperado)
    {
        var paginacion = new Paginacion(pagina, tamano);

        Assert.Equal(paginaEsperada, paginacion.Pagina);
        Assert.Equal(tamanoEsperado, paginacion.Tamano);
        Assert.Equal((paginaEsperada - 1) * tamanoEsperado, paginacion.Salto);
    }

    [Fact]
    public void Convertir_ConservaTotalYPagina()
    {
        var pagina = new PaginaResultado<int>([1, 2], 7, new Paginacion(2, 2)).Convertir(n => n.ToString());

        Assert.Equal(["1", "2"], pagina.Elementos);
        Assert.Equal(7, pagina.Total);
        Assert.Equal(2, pagina.Paginacion.Pagina);
    }
}
