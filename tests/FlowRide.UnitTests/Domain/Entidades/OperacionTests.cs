using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.UnitTests.Domain.Entidades.Datos;

namespace FlowRide.UnitTests.Domain.Entidades;

public class SolicitudViajeTests
{
    [Fact]
    public void NuevaSolicitud_PendienteConHistorialInicial()
    {
        var solicitud = Solicitud();

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        var inicial = Assert.Single(solicitud.Historial);
        Assert.Equal((null, EstadoSolicitud.Pendiente, Id(10)), (inicial.EstadoAnterior, inicial.EstadoNuevo, inicial.ActorId));
        Assert.Equal(new DateOnly(2026, 10, 14), solicitud.FechaServicio);
        Assert.Equal(TimeSpan.FromMinutes(45), solicitud.Franja().Duracion);
    }

    [Fact]
    public void CicloCompleto_QuedaRegistradoEnElHistorial()
    {
        var solicitud = Solicitud();
        var asignacion = Asignacion(solicitud);

        solicitud.MarcarAsignada(asignacion, null, Ahora.AddMinutes(1));
        solicitud.MarcarEnCurso(Ahora.AddHours(4));
        solicitud.MarcarCompletada(Ahora.AddHours(5));

        Assert.Equal(EstadoSolicitud.Completada, solicitud.Estado);
        Assert.Equal(
            new EstadoSolicitud[] { EstadoSolicitud.Pendiente, EstadoSolicitud.Asignada, EstadoSolicitud.EnCurso, EstadoSolicitud.Completada },
            solicitud.Historial.Select(h => h.EstadoNuevo));
    }

    [Fact]
    public void SinCandidatos_YVuelveAPendiente()
    {
        var solicitud = Solicitud();

        solicitud.MarcarSinCandidatos("Ningún conductor disponible", Ahora);
        Assert.Equal(EstadoSolicitud.SinCandidatos, solicitud.Estado);

        solicitud.VolverAPendiente("Nuevo turno disponible", OperadorId, Ahora.AddMinutes(10));
        var ultimo = solicitud.Historial[^1];
        Assert.Equal((EstadoSolicitud.SinCandidatos, EstadoSolicitud.Pendiente, OperadorId, "Nuevo turno disponible"),
            (ultimo.EstadoAnterior, ultimo.EstadoNuevo, ultimo.ActorId, ultimo.Motivo));
    }

    [Fact]
    public void AsignacionDeOtraSolicitud_SeRechaza()
    {
        var solicitud = Solicitud();
        var ajena = new Asignacion(Id(41), Id(99), Id(20), Id(50), solicitud.Franja(), OrigenAsignacion.Manual, Ahora);

        Assert.Throws<InvalidOperationException>(() => solicitud.MarcarAsignada(ajena, null, Ahora));
    }

    [Fact]
    public void Cancelar_RegistraLaEvidencia_YNoSePuedeCancelarDosVeces()
    {
        var solicitud = Solicitud();

        var cancelacion = solicitud.Cancelar(Id(90), "Vuelo cancelado", Id(10), Ahora.AddMinutes(5));

        Assert.Equal(EstadoSolicitud.Cancelada, solicitud.Estado);
        Assert.Same(cancelacion, solicitud.Cancelacion);
        Assert.Equal(("Vuelo cancelado", Id(10)), (cancelacion.Motivo, cancelacion.CanceladaPorId));
        Assert.Throws<InvalidOperationException>(() => solicitud.Cancelar(Id(91), "x", Id(10), Ahora));
    }

    [Fact]
    public void TransicionesInvalidas_Fallan()
    {
        var solicitud = Solicitud();

        Assert.Throws<InvalidOperationException>(() => solicitud.MarcarEnCurso(Ahora));
        Assert.Throws<InvalidOperationException>(() => solicitud.MarcarCompletada(Ahora));
        Assert.Throws<InvalidOperationException>(() => solicitud.VolverAPendiente("x", null, Ahora));

        solicitud.MarcarAsignada(Asignacion(solicitud), null, Ahora);
        solicitud.MarcarEnCurso(Ahora);
        Assert.Throws<InvalidOperationException>(() => solicitud.Cancelar(Id(90), "tarde", Id(10), Ahora));
        Assert.Throws<InvalidOperationException>(() => solicitud.MarcarSinCandidatos("x", Ahora));
    }

    [Fact]
    public void DatosInvalidos_Fallan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SolicitudViaje(Id(30), Id(10), "O", "D", Ahora,
            TimeSpan.Zero, 1, CanalSolicitud.PanelWeb, Ahora));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SolicitudViaje(Id(30), Id(10), "O", "D", Ahora,
            TimeSpan.FromMinutes(10), 0, CanalSolicitud.PanelWeb, Ahora));
        Assert.Throws<ArgumentException>(() => new SolicitudViaje(Id(30), Guid.Empty, "O", "D", Ahora,
            TimeSpan.FromMinutes(10), 1, CanalSolicitud.PanelWeb, Ahora));
    }
}

public class AsignacionTests
{
    [Fact]
    public void Automatica_RequiereDecisionDelMotor() =>
        Assert.Throws<ArgumentException>(() => new Asignacion(Id(40), Id(30), Id(20), Id(50), Franja(4, 5),
            OrigenAsignacion.Automatica, Ahora));

    [Fact]
    public void CicloAceptarIniciarFinalizar_ElViajeNaceAlAceptar()
    {
        var asignacion = Asignacion();
        Assert.True(asignacion.EstaVigente);

        var viaje = asignacion.Aceptar(Id(70));
        Assert.Equal((EstadoAsignacion.Aceptada, EstadoViaje.Programado, asignacion.Id),
            (asignacion.Estado, viaje.Estado, viaje.AsignacionId));

        asignacion.Iniciar(viaje, Ahora.AddHours(4));
        Assert.Equal((EstadoAsignacion.EnCurso, EstadoViaje.EnCurso), (asignacion.Estado, viaje.Estado));

        asignacion.Finalizar(viaje, Ahora.AddHours(5));
        Assert.Equal((EstadoAsignacion.Finalizada, EstadoViaje.Finalizado), (asignacion.Estado, viaje.Estado));
        Assert.False(asignacion.EstaVigente);
    }

    [Fact]
    public void RechazarYCancelar_GuardanElMotivo()
    {
        var rechazada = Asignacion();
        rechazada.Rechazar("Sin combustible");
        Assert.Equal((EstadoAsignacion.Rechazada, "Sin combustible"), (rechazada.Estado, rechazada.MotivoCierre));

        var cancelada = Asignacion();
        cancelada.Aceptar(Id(70));
        cancelada.Cancelar("Solicitud cancelada");
        Assert.Equal(EstadoAsignacion.Cancelada, cancelada.Estado);
        Assert.Throws<InvalidOperationException>(() => cancelada.Aceptar(Id(71)));
    }

    [Fact]
    public void Reemplazar_CreaAsignacionManualEncadenada()
    {
        var original = Asignacion();
        original.Aceptar(Id(70));

        var nueva = original.Reemplazar(Id(42), Id(21), Id(51), "El conductor abandonó el turno", Ahora.AddMinutes(30));

        Assert.Equal(EstadoAsignacion.Reemplazada, original.Estado);
        Assert.False(original.EstaVigente);
        Assert.Equal((OrigenAsignacion.Manual, original.Id, original.SolicitudId, original.Franja, (Guid?)null),
            (nueva.Origen, nueva.AsignacionReemplazadaId, nueva.SolicitudId, nueva.Franja, nueva.DecisionId));
        Assert.Equal(EstadoAsignacion.Asignada, nueva.Estado);
    }

    [Fact]
    public void Reemplazar_SinCambiarConductorNiVehiculo_Falla()
    {
        var original = Asignacion();

        Assert.Throws<InvalidOperationException>(() => original.Reemplazar(Id(42), Id(20), Id(50), "x", Ahora));
    }

    [Fact]
    public void NoSePuedeReemplazarNiCancelarUnaAsignacionEnCurso()
    {
        var asignacion = Asignacion();
        asignacion.Iniciar(asignacion.Aceptar(Id(70)), Ahora.AddHours(4));

        Assert.Throws<InvalidOperationException>(() => asignacion.Reemplazar(Id(42), Id(21), Id(51), "x", Ahora));
        Assert.Throws<InvalidOperationException>(() => asignacion.Cancelar("x"));
        Assert.Throws<InvalidOperationException>(() => asignacion.Rechazar("x"));
    }
}

public class ViajeTests
{
    [Fact]
    public void Finalizar_RegistraLasFechas()
    {
        var viaje = ViajeEnCurso();

        viaje.Finalizar(Ahora.AddHours(5));

        Assert.Equal((EstadoViaje.Finalizado, Ahora.AddHours(4), Ahora.AddHours(5)),
            (viaje.Estado, viaje.FechaHoraInicio, viaje.FechaHoraFin));
        Assert.Throws<InvalidOperationException>(() => viaje.Finalizar(Ahora.AddHours(6)));
        Assert.Throws<InvalidOperationException>(viaje.Cancelar);
    }

    [Fact]
    public void FinalizarAntesDelInicio_Falla() =>
        Assert.Throws<ArgumentException>(() => ViajeEnCurso().Finalizar(Ahora));

    [Fact]
    public void Paradas_OrdenUnico_YLlegadaUnaSolaVez()
    {
        var viaje = ViajeEnCurso();

        var parada = viaje.RegistrarParada(Id(71), "Terminal 2", 1, Ahora.AddHours(4.5));
        Assert.Throws<InvalidOperationException>(() => viaje.RegistrarParada(Id(72), "Otra", 1, Ahora));

        parada.RegistrarLlegada(Ahora.AddHours(4.6));
        Assert.Equal(Ahora.AddHours(4.6), parada.HoraReal);
        Assert.Throws<InvalidOperationException>(() => parada.RegistrarLlegada(Ahora));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Parada(Id(73), viaje.Id, 0, "x", Ahora));
    }

    [Fact]
    public void Incidentes_SoloEnViajesAbiertos()
    {
        var viaje = ViajeEnCurso();

        var incidente = viaje.RegistrarIncidente(Id(74), TipoIncidente.Retraso, "Trancón en la autopista", Id(20), Ahora);
        Assert.Equal((TipoIncidente.Retraso, Id(20)), (incidente.Tipo, incidente.ReportadoPorId));

        viaje.Cancelar();
        Assert.Equal(EstadoViaje.Cancelado, viaje.Estado);
        Assert.Throws<InvalidOperationException>(() =>
            viaje.RegistrarIncidente(Id(75), TipoIncidente.Otro, "x", Id(20), Ahora));
    }

    [Fact]
    public void ViajeProgramado_SePuedeIniciarUnaVez()
    {
        var viaje = new Viaje(Id(70), Id(40));

        viaje.Iniciar(Ahora);
        Assert.Throws<InvalidOperationException>(() => viaje.Iniciar(Ahora));
    }
}

public class CalificacionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void PuntajeFueraDeRango_Falla(int puntaje) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Calificacion(Id(80), Id(70), Id(10), puntaje, null, Ahora));

    [Fact]
    public void ComentarioVacio_QuedaNulo() =>
        Assert.Null(new Calificacion(Id(80), Id(70), Id(10), 5, "   ", Ahora).Comentario);
}
