using FlowRide.Domain.Comun;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Reportes;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Domain.Reportes;

public class ReportesDominioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly ZonaHorariaOperador Bogota = new(TimeZoneInfo.CreateCustomTimeZone("Bogota", TimeSpan.FromHours(-5), "Bogotá", "Bogotá"));

    [Fact]
    public void Periodo_ExigeOrdenYMaximoDe93Dias()
    {
        Assert.Throws<FlowRideExcepcion>(() => new PeriodoReporte(Ahora, Ahora));
        Assert.Throws<FlowRideExcepcion>(() => new PeriodoReporte(Ahora, Ahora.AddDays(PeriodoReporte.DiasMaximos).AddMinutes(1)));

        var periodo = new PeriodoReporte(Ahora, Ahora.AddDays(PeriodoReporte.DiasMaximos));
        Assert.False(periodo.CerradoEn(Ahora));
        Assert.True(periodo.CerradoEn(periodo.Hasta));
    }

    [Fact]
    public void Periodo_DelDia_UsaLaZonaDelOperador()
    {
        var dia = PeriodoReporte.DelDia(new DateOnly(2026, 10, 18), Bogota);

        Assert.Equal(new DateTimeOffset(2026, 10, 18, 5, 0, 0, TimeSpan.Zero), dia.Desde.ToUniversalTime());
        Assert.Equal(TimeSpan.FromDays(1), dia.Hasta - dia.Desde);
        Assert.Throws<ArgumentNullException>(() => PeriodoReporte.DelDia(new DateOnly(2026, 10, 18), null!));
    }

    [Theory]
    [InlineData(EstadoSolicitud.Completada, GrupoSolicitud.Atendida)]
    [InlineData(EstadoSolicitud.EnCurso, GrupoSolicitud.Atendida)]
    [InlineData(EstadoSolicitud.Cancelada, GrupoSolicitud.Cancelada)]
    [InlineData(EstadoSolicitud.Pendiente, GrupoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.SinCandidatos, GrupoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Asignada, GrupoSolicitud.Pendiente)]
    public void Grupo_DeCadaEstado(EstadoSolicitud estado, GrupoSolicitud esperado) => Assert.Equal(esperado, ReglasReportes.Grupo(estado));

    [Fact]
    public void Puntualidad_RetrasoYPorcentajes()
    {
        var tolerancia = TimeSpan.FromMinutes(10);
        Assert.True(ReglasReportes.Puntual(Ahora, Ahora.AddMinutes(10), tolerancia));
        Assert.False(ReglasReportes.Puntual(Ahora, Ahora.AddMinutes(11), tolerancia));
        Assert.False(ReglasReportes.Puntual(Ahora, null, tolerancia));
        Assert.Equal((0, 0, 25), (ReglasReportes.MinutosRetraso(Ahora, null), ReglasReportes.MinutosRetraso(Ahora, Ahora.AddMinutes(-5)),
            ReglasReportes.MinutosRetraso(Ahora, Ahora.AddMinutes(25))));

        Assert.Null(ReglasReportes.Porcentaje(0, 0));
        Assert.Equal(33.3m, ReglasReportes.Porcentaje(1, 3));
        Assert.Null(ReglasReportes.Promedio([]));
        Assert.Equal(4.5m, ReglasReportes.Promedio([4, 5]));
    }

    [Fact]
    public void ValidarFilas_RechazaLosPeriodosDemasiadoGrandes()
    {
        ReglasReportes.ValidarFilas(ReglasReportes.FilasMaximas);
        Assert.Throws<FlowRideExcepcion>(() => ReglasReportes.ValidarFilas(ReglasReportes.FilasMaximas + 1));
    }

    public static TheoryData<ServicioEnCurso, NivelAtencion> Servicios => new()
    {
        { Servicio(EstadoSolicitud.Completada), NivelAtencion.Normal },
        { Servicio(EstadoSolicitud.Cancelada, minutos: -30), NivelAtencion.Normal },
        { Servicio(EstadoSolicitud.SinCandidatos), NivelAtencion.Critico },
        { Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Aceptada, EstadoViaje.Programado, minutos: -5), NivelAtencion.Critico },
        { Servicio(EstadoSolicitud.Pendiente, minutos: 20), NivelAtencion.Critico },
        { Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Asignada, minutos: 30), NivelAtencion.Critico },
        { Servicio(EstadoSolicitud.Pendiente), NivelAtencion.Atencion },
        { Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Asignada), NivelAtencion.Atencion },
        { Servicio(EstadoSolicitud.EnCurso, EstadoAsignacion.EnCurso, EstadoViaje.EnCurso, incidentes: 1), NivelAtencion.Atencion },
        { Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Aceptada, EstadoViaje.Programado, urgente: true), NivelAtencion.Atencion },
        { Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Aceptada, EstadoViaje.Programado), NivelAtencion.Normal }
    };

    [Theory]
    [MemberData(nameof(Servicios))]
    public void Nivel_Semaforiza(ServicioEnCurso servicio, NivelAtencion esperado)
    {
        var (nivel, motivo) = ReglasReportes.Nivel(servicio, Ahora);

        Assert.Equal(esperado, nivel);
        Assert.Equal(nivel == NivelAtencion.Normal, motivo is null);
    }

    [Fact]
    public void Nivel_ExplicaElRetraso()
    {
        var (_, motivo) = ReglasReportes.Nivel(Servicio(EstadoSolicitud.Asignada, EstadoAsignacion.Aceptada, EstadoViaje.Programado, minutos: -12),
            Ahora);

        Assert.Equal("Retrasado 12 min.", motivo);
        Assert.Throws<ArgumentNullException>(() => ReglasReportes.Nivel(null!, Ahora));
    }

    private static ServicioEnCurso Servicio(EstadoSolicitud estado, EstadoAsignacion? asignacion = null, EstadoViaje? viaje = null,
        int minutos = 180, bool urgente = false, int incidentes = 0) =>
        new(estado, asignacion, viaje, Ahora.AddMinutes(minutos), urgente, incidentes);
}
