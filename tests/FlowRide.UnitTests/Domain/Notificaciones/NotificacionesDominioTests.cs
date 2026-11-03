using FlowRide.Domain.Canal;
using FlowRide.Domain.Notificaciones;

namespace FlowRide.UnitTests.Domain.Notificaciones;

public class NotificacionesDominioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VentanaDe24Horas()
    {
        Assert.False(VentanaConversacion.Abierta(null, Ahora));
        Assert.True(VentanaConversacion.Abierta(Ahora.AddHours(-23), Ahora));
        Assert.False(VentanaConversacion.Abierta(Ahora.AddHours(-24), Ahora));
    }

    [Fact]
    public void Alerta_SeMarcaLeidaUnaSolaVez()
    {
        var alerta = new Alerta(Guid.NewGuid(), "alerta.x", SeveridadAlerta.Critica, "Título", "Detalle", Ahora);
        var lector = Guid.NewGuid();

        Assert.True(alerta.MarcarLeida(lector, Ahora));
        Assert.False(alerta.MarcarLeida(Guid.NewGuid(), Ahora.AddMinutes(1)));
        Assert.Equal((Ahora, (Guid?)lector), (alerta.LeidaEn, alerta.LeidaPorId));
        Assert.Throws<ArgumentException>(() => new Alerta(Guid.NewGuid(), " ", SeveridadAlerta.Info, "t", "d", Ahora));
    }

    [Fact]
    public void Correo_ReintentaConEsperaCreciente_YAlQuintoQuedaFallido()
    {
        var correo = new CorreoSaliente(Guid.NewGuid(), "admin@a.co", "Asunto", "Cuerpo", Ahora);
        Assert.True(correo.ListoParaEnviar(Ahora));

        correo.RegistrarFallo("SMTP caído", Ahora);
        Assert.Equal(Ahora + MensajeSaliente.Esperas[0], correo.ProximoIntento);
        for (var i = 1; i < CorreoSaliente.MaximoIntentos; i++) correo.RegistrarFallo("SMTP caído", Ahora);

        Assert.Equal(EstadoCorreo.Fallido, correo.Estado);
        Assert.Throws<InvalidOperationException>(() => correo.MarcarEnviado(Ahora));
        var enviado = new CorreoSaliente(Guid.NewGuid(), "admin@a.co", "Asunto", "Cuerpo", Ahora);
        enviado.MarcarEnviado(Ahora);
        Assert.Equal((EstadoCorreo.Enviado, (DateTimeOffset?)Ahora), (enviado.Estado, enviado.EnviadoEn));
    }

    [Fact]
    public void Plantilla_SoloLaAprobadaSeUsa()
    {
        var plantilla = new PlantillaWhatsApp("viaje.pasajero.iniciado", "viaje_pasajero_iniciado", "es_CO");
        Assert.False(plantilla.Utilizable);

        plantilla.Actualizar("viaje_iniciado", "es", EstadoPlantilla.Aprobada, Guid.NewGuid(), Ahora);
        Assert.True(plantilla.Utilizable);
        plantilla.Actualizar("viaje_iniciado", "es", EstadoPlantilla.Rechazada, Guid.NewGuid(), Ahora);
        Assert.False(plantilla.Utilizable);
    }

    [Fact]
    public void MensajeSaliente_ConPlantillaYOrigen()
    {
        var eventId = Guid.NewGuid();
        var mensaje = new MensajeSaliente(Guid.NewGuid(), "+57300", "Texto", Ahora).ComoNotificacion("viaje.pasajero.iniciado", eventId);
        Assert.False(mensaje.EsPlantilla);

        mensaje.ConPlantilla("viaje_iniciado", "es_CO", "[\"Luis\"]");

        Assert.Equal((true, "viaje.pasajero.iniciado", (Guid?)eventId, "[\"Luis\"]"),
            (mensaje.EsPlantilla, mensaje.ClaveMensaje, mensaje.EventId, mensaje.ParametrosPlantilla));
    }
}
