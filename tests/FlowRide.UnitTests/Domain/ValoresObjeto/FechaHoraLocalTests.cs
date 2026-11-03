using FlowRide.Application.Comun;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.UnitTests.Domain.ValoresObjeto;

public class FechaHoraLocalTests
{
    private static readonly DateTime OchoDeLaManana = new(2026, 10, 21, 8, 0, 37, DateTimeKind.Unspecified);

    [Fact]
    public void Guarda_LaHoraLocalAlMinuto_YElNombreDeLaZona()
    {
        var hora = new FechaHoraLocal(OchoDeLaManana, "America/Bogota");

        Assert.Equal((new DateTime(2026, 10, 21, 8, 0, 0), "America/Bogota"), (hora.Hora, hora.Zona));
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 13, 0, 0, TimeSpan.Zero), hora.Instante());
        Assert.Equal(new DateOnly(2026, 10, 21), hora.Fecha);
        Assert.Equal("2026-10-21 08:00 (America/Bogota)", hora.ToString());
        Assert.Equal(hora.Instante().AddMinutes(45), hora.Mas(TimeSpan.FromMinutes(45)).Instante());
    }

    [Theory]
    [InlineData("-05:00")]
    [InlineData("UTC-5")]
    [InlineData("Marte/Olympus")]
    [InlineData("")]
    public void RechazaLoQueNoEsUnNombreIanaDeZona(string zona) =>
        Assert.Throws<FlowRideExcepcion>(() => new FechaHoraLocal(OchoDeLaManana, zona));

    [Fact]
    public void RechazaUnaHoraConDesfase_LaZonaVaPorNombre()
    {
        Assert.Throws<FlowRideExcepcion>(() => new FechaHoraLocal(DateTime.SpecifyKind(OchoDeLaManana, DateTimeKind.Utc), "America/Bogota"));
        Assert.Throws<FlowRideExcepcion>(() => new FechaHoraLocal(DateTime.SpecifyKind(OchoDeLaManana, DateTimeKind.Local), "America/Bogota"));
    }

    [Fact]
    public void HoraQueNoExiste_PorElCambioDeHorario_SeRechaza() =>
        // En Nueva York el reloj salta de las 2:00 a las 3:00 el 8 de marzo de 2026.
        Assert.Throws<FlowRideExcepcion>(() => new FechaHoraLocal(new DateTime(2026, 3, 8, 2, 30, 0), "America/New_York"));

    [Fact]
    public void HoraQueOcurreDosVeces_TomaLaPrimera()
    {
        // El 1 de noviembre de 2026 la 1:30 ocurre dos veces en Nueva York: primero con -4 (verano) y luego con -5.
        var ambigua = new FechaHoraLocal(new DateTime(2026, 11, 1, 1, 30, 0), "America/New_York");

        Assert.Equal(new DateTimeOffset(2026, 11, 1, 5, 30, 0, TimeSpan.Zero), ambigua.Instante());
    }

    [Fact]
    public void MismaHoraLocal_DistintoInstanteSegunLasReglasDeLaFecha()
    {
        // Las 9:00 en Nueva York son 13:00 UTC en verano y 14:00 UTC en invierno: lo que se guarda es "9:00 en Nueva York".
        Assert.Equal(13, new FechaHoraLocal(new DateTime(2026, 7, 1, 9, 0, 0), "America/New_York").Instante().Hour);
        Assert.Equal(14, new FechaHoraLocal(new DateTime(2026, 12, 1, 9, 0, 0), "America/New_York").Instante().Hour);
    }

    [Fact]
    public void De_PasaUnInstanteAHoraLocal()
    {
        var local = FechaHoraLocal.De(new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero), "America/Bogota");

        Assert.Equal((new DateTime(2026, 10, 31, 20, 0, 0), new DateOnly(2026, 10, 31)), (local.Hora, local.Fecha));
    }

    [Fact]
    public void HorasLocales_UsaLaZonaDelOperadorSiNoSeIndica()
    {
        Assert.Null(HorasLocales.Opcional(null, null, HorasDePrueba.ZonaOperador));
        Assert.Equal("America/Bogota", HorasLocales.Opcional(OchoDeLaManana, " ", HorasDePrueba.ZonaOperador)!.Zona);
        Assert.Equal("Europe/Madrid", HorasLocales.Opcional(OchoDeLaManana, "Europe/Madrid", HorasDePrueba.ZonaOperador)!.Zona);
        Assert.Throws<ArgumentNullException>(() => HorasLocales.Opcional(OchoDeLaManana, null, null!));
    }
}
