using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;
using static FlowRide.UnitTests.Domain.Entidades.Datos;

namespace FlowRide.UnitTests.Domain.Entidades;

public class FranjaHorariaTests
{
    [Fact]
    public void FinDebeSerPosteriorAlInicio() =>
        Assert.Throws<ArgumentException>(() => new FranjaHoraria(Ahora, Ahora));

    [Fact]
    public void ContieneEsSemiabierto()
    {
        var franja = Franja(0, 2);

        Assert.True(franja.Contiene(Ahora));
        Assert.False(franja.Contiene(Ahora.AddHours(2)));
        Assert.Equal(TimeSpan.FromHours(2), franja.Duracion);
    }

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(2, 4, false)]
    [InlineData(-2, 0, false)]
    public void SeSolapaCon(int desde, int hasta, bool esperado) =>
        Assert.Equal(esperado, Franja(0, 2).SeSolapaCon(Franja(desde, hasta)));

    [Fact]
    public void EstaContenidaEn()
    {
        Assert.True(Franja(1, 2).EstaContenidaEn(Franja(0, 3)));
        Assert.False(Franja(1, 4).EstaContenidaEn(Franja(0, 3)));
    }
}

public class ConductorTests
{
    [Fact]
    public void EstaActivo_RequiereUsuarioActivoYEstadoOperativoActivo()
    {
        var conductor = Conductor();
        Assert.True(conductor.EstaActivo);

        conductor.CambiarEstado(EstadoConductor.Suspendido);
        Assert.False(conductor.EstaActivo);

        conductor.CambiarEstado(EstadoConductor.Activo);
        conductor.Desactivar();
        Assert.False(conductor.EstaActivo);
    }

    [Fact]
    public void ProgramarTurno_RechazaSolapamientoConTurnoVigente()
    {
        var conductor = Conductor();
        conductor.ProgramarTurno(Id(100), Franja(0, 8));

        Assert.Throws<InvalidOperationException>(() => conductor.ProgramarTurno(Id(101), Franja(7, 12)));
        var siguiente = conductor.ProgramarTurno(Id(102), Franja(8, 16));

        Assert.Equal(2, conductor.Turnos.Count);
        Assert.Equal(conductor.Id, siguiente.ConductorId);
    }

    [Fact]
    public void TurnoAbandonado_LiberaLaFranja_YNoCubreServicios()
    {
        var conductor = Conductor();
        var turno = conductor.ProgramarTurno(Id(100), Franja(0, 8));
        Assert.True(conductor.TieneTurnoQueCubre(Franja(1, 2)));

        conductor.AbandonarTurno(turno, "Enfermedad");

        Assert.Equal(EstadoTurno.Abandonado, turno.Estado);
        Assert.False(conductor.TieneTurnoQueCubre(Franja(1, 2)));
        conductor.ProgramarTurno(Id(101), Franja(0, 8));
    }

    [Fact]
    public void AbandonarTurnoAjeno_Falla()
    {
        var conductor = Conductor();
        var ajeno = new Turno(Id(100), Id(99), Ahora.Local(), Ahora.AddHours(8).Local());

        Assert.Throws<InvalidOperationException>(() => conductor.AbandonarTurno(ajeno, "x"));
    }

    [Fact]
    public void AceptarYRechazarAsignaciones_SoloLasPropias()
    {
        var conductor = Conductor();
        var propia = Asignacion();
        var ajena = new Asignacion(Id(41), Id(30), Id(99), Id(50), Franja(4, 5), OrigenAsignacion.Manual, Ahora);

        var viaje = conductor.AceptarAsignacion(propia, Id(70));
        Assert.Equal((EstadoAsignacion.Aceptada, propia.Id), (propia.Estado, viaje.AsignacionId));
        Assert.Throws<InvalidOperationException>(() => conductor.RechazarAsignacion(ajena, "No"));

        var otra = Asignacion();
        conductor.RechazarAsignacion(otra, "Vehículo en taller");
        Assert.Equal(EstadoAsignacion.Rechazada, otra.Estado);
    }

    [Fact]
    public void RegistrarAsignacion_ConservaLaMasReciente()
    {
        var conductor = Conductor();

        conductor.RegistrarAsignacion(Ahora);
        conductor.RegistrarAsignacion(Ahora.AddHours(-3));

        Assert.Equal(Ahora, conductor.FechaUltimaAsignacion);
    }

    [Fact]
    public void LicenciaYDocumentos()
    {
        var conductor = Conductor();
        conductor.ActualizarLicencia("LIC-NUEVA", "C3", Hoy.AddDays(-1));
        var documento = conductor.AgregarDocumento(Id(110), TipoDocumento.Licencia, "documents/lic.pdf", Hoy.AddDays(10));

        Assert.False(conductor.LicenciaVigenteEn(Hoy));
        Assert.Equal(("LIC-NUEVA", "C3"), (conductor.NumeroLicencia, conductor.CategoriaLicencia));
        Assert.Single(conductor.Documentos);
        Assert.True(documento.VigenteEn(Hoy));
        Assert.False(documento.VigenteEn(Hoy.AddDays(11)));
        Assert.True(new DocumentoConductor(Id(111), conductor.Id, TipoDocumento.Otro, "documents/x", null).VigenteEn(Hoy));
    }
}

public class TurnoTests
{
    [Fact]
    public void CicloProgramadoActivoFinalizado()
    {
        var turno = new Turno(Id(100), Id(20), Ahora.Local(), Ahora.AddHours(8).Local());

        turno.Iniciar();
        Assert.Equal(EstadoTurno.Activo, turno.Estado);
        turno.Finalizar();
        Assert.Equal(EstadoTurno.Finalizado, turno.Estado);

        Assert.Throws<InvalidOperationException>(turno.Iniciar);
        Assert.Throws<InvalidOperationException>(turno.Finalizar);
        Assert.Throws<InvalidOperationException>(() => turno.MarcarAbandonado("x"));
    }

    [Fact]
    public void CubreUnaFranjaSoloSiCabeCompleta()
    {
        var turno = new Turno(Id(100), Id(20), Ahora.Local(), Ahora.AddHours(8).Local());

        Assert.True(turno.Cubre(Franja(1, 2)));
        Assert.False(turno.Cubre(Franja(7, 9)));
    }

    [Fact]
    public void MarcarAbandonado_GuardaElMotivo()
    {
        var turno = new Turno(Id(100), Id(20), Ahora.Local(), Ahora.AddHours(8).Local());

        turno.MarcarAbandonado("Emergencia familiar");

        Assert.Equal("Emergencia familiar", turno.MotivoAbandono);
        Assert.False(turno.Cubre(Ahora.AddHours(1)));
    }
}

public class VehiculoTests
{
    private static Vehiculo Nuevo() => new(Id(50), "abc123", "Toyota", "Hiace", 12, Hoy.AddYears(1), Hoy.AddYears(1));

    [Fact]
    public void PlacaEnMayusculas_YCapacidadPositiva()
    {
        Assert.Equal("ABC123", Nuevo().Placa);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Vehiculo(Id(50), "ABC123", "T", "H", 0, Hoy, Hoy));
    }

    [Fact]
    public void ActivacionDocumentacionYDatos()
    {
        var vehiculo = Nuevo();

        vehiculo.Desactivar();
        Assert.False(vehiculo.Activo);
        vehiculo.Activar();

        vehiculo.RenovarDocumentacion(Hoy.AddDays(-1), Hoy.AddYears(1));
        Assert.False(vehiculo.DocumentacionVigenteEn(Hoy));

        vehiculo.ActualizarDatos("Mercedes", "Sprinter", 19);
        Assert.Equal(("Mercedes", "Sprinter", 19), (vehiculo.Marca, vehiculo.Modelo, vehiculo.Capacidad));
        Assert.True(vehiculo.AdmiteCapacidad(19));
    }

    [Fact]
    public void ConductorHabitual_OpcionalPeroNoVacio()
    {
        var vehiculo = Nuevo();

        vehiculo.AsignarConductorHabitual(Id(20));
        Assert.Equal(Id(20), vehiculo.ConductorHabitualId);
        vehiculo.AsignarConductorHabitual(null);
        Assert.Null(vehiculo.ConductorHabitualId);
        Assert.Throws<ArgumentException>(() => vehiculo.AsignarConductorHabitual(Guid.Empty));
    }
}
