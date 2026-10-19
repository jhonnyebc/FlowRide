using FlowRide.Domain.Canal;

namespace FlowRide.UnitTests.Domain.Canal;

public class EntradaConversacionTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);

    [Theory]
    [InlineData("menu", true)]
    [InlineData("  MENÚ ", true)]
    [InlineData("Hola", true)]
    [InlineData("cancelar", true)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void PideMenu(string texto, bool esperado) => Assert.Equal(esperado, EntradaConversacion.PideMenu(texto));

    [Theory]
    [InlineData("3", 1, 4, 3)]
    [InlineData(" 2. ", 1, 4, 2)]
    [InlineData("5", 1, 4, null)]
    [InlineData("dos", 1, 4, null)]
    [InlineData("-1", 1, 4, null)]
    public void Numero(string texto, int minimo, int maximo, int? esperado) =>
        Assert.Equal(esperado, EntradaConversacion.Numero(texto, minimo, maximo));

    [Theory]
    [InlineData("hoy 14:00", "2026-10-18", "14:00")]
    [InlineData("Mañana 7:30", "2026-10-19", "07:30")]
    [InlineData("manana a las 7", "2026-10-19", "07:00")]
    [InlineData("25/10 06:15", "2026-10-25", "06:15")]
    [InlineData("25-10-2026 6:15 pm", "2026-10-25", "18:15")]
    [InlineData("1/3 12 am", "2027-03-01", "00:00")]
    [InlineData("01/10 08:00", "2027-10-01", "08:00")]
    public void FechaHora_Entendidas(string texto, string fecha, string hora)
    {
        var resultado = EntradaConversacion.FechaHora(texto, Hoy);

        Assert.Equal((DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture),
            TimeOnly.Parse(hora, System.Globalization.CultureInfo.InvariantCulture)), resultado);
    }

    [Theory]
    [InlineData("pasado mañana 7")]
    [InlineData("31/02 10:00")]
    [InlineData("hoy 25:00")]
    [InlineData("hoy 10:75")]
    [InlineData("13/13 10:00")]
    [InlineData("")]
    public void FechaHora_NoEntendidas(string texto) => Assert.Null(EntradaConversacion.FechaHora(texto, Hoy));

    [Fact]
    public void TelefonoE164_SoloDigitosConMas()
    {
        Assert.Equal("+573001112233", EntradaConversacion.TelefonoE164("573001112233"));
        Assert.Equal("+573001112233", EntradaConversacion.TelefonoE164("+57 300 111 2233"));
        Assert.Throws<ArgumentException>(() => EntradaConversacion.TelefonoE164("sin número"));
    }
}

public class ConversacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GuardaDatosYOpciones_YSeReinicia()
    {
        var conversacion = new Conversacion(Guid.NewGuid(), "+573001112233", Ahora);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        conversacion.Guardar("origen", "Hotel");
        conversacion.GuardarOpciones(ids);
        conversacion.Ir(PasoConversacion.PedirDestino, Ahora.AddMinutes(1));

        Assert.Equal(("Hotel", ids[1], (Guid?)null, (Guid?)null), (conversacion.Leer("origen"), conversacion.Opcion(2), conversacion.Opcion(3),
            conversacion.Opcion(0)));
        Assert.Contains("Hotel", conversacion.Datos, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => conversacion.Requerido("destino"));

        conversacion.Reiniciar(Ahora.AddMinutes(2));
        Assert.Equal((PasoConversacion.Menu, null), (conversacion.Paso, conversacion.Leer("origen")));
        Assert.Equal("{}", conversacion.Datos);
    }

    [Fact]
    public void ExpiraTrasMediaHoraSinMensajes()
    {
        var conversacion = new Conversacion(Guid.NewGuid(), "+573001112233", Ahora);

        Assert.False(conversacion.Expirada(Ahora + Conversacion.Inactividad));
        Assert.True(conversacion.Expirada(Ahora + Conversacion.Inactividad + TimeSpan.FromSeconds(1)));
    }
}

public class MensajesCanalTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Entrante_SeProcesaUnaVez_YAlQuintoFalloQuedaFallido()
    {
        var procesado = new MensajeEntrante(Guid.NewGuid(), "wamid.1", "+57300", "texto", " hola ", Ahora);
        procesado.MarcarProcesado(Guid.NewGuid(), Ahora);
        Assert.Equal(("hola", EstadoMensajeEntrante.Procesado), (procesado.Contenido, procesado.Estado));
        Assert.Throws<InvalidOperationException>(() => procesado.MarcarProcesado(null, Ahora));
        procesado.RegistrarFallo("no aplica");
        Assert.Equal(0, procesado.Intentos);

        var conFallos = new MensajeEntrante(Guid.NewGuid(), "wamid.2", "+57300", "texto", "hola", Ahora);
        for (var i = 0; i < MensajeEntrante.MaximoIntentos - 1; i++) conFallos.RegistrarFallo("timeout");
        Assert.Equal(EstadoMensajeEntrante.Pendiente, conFallos.Estado);
        conFallos.RegistrarFallo("timeout");
        Assert.Equal((EstadoMensajeEntrante.Fallido, MensajeEntrante.MaximoIntentos, "timeout"), (conFallos.Estado, conFallos.Intentos, conFallos.UltimoError));
    }

    [Fact]
    public void Saliente_ReintentaConEsperaCreciente_YAlQuintoQuedaFallido()
    {
        var mensaje = new MensajeSaliente(Guid.NewGuid(), "+57300", "Hola", Ahora);
        Assert.True(mensaje.ListoParaEnviar(Ahora));

        mensaje.ProgramarReintento("HTTP 503", Ahora);
        Assert.Equal(Ahora + MensajeSaliente.Esperas[0], mensaje.ProximoIntento);
        Assert.False(mensaje.ListoParaEnviar(Ahora));
        mensaje.ProgramarReintento("HTTP 503", Ahora);
        mensaje.ProgramarReintento("HTTP 503", Ahora);
        mensaje.ProgramarReintento("HTTP 503", Ahora);
        Assert.Equal(Ahora + MensajeSaliente.Esperas[3], mensaje.ProximoIntento);
        mensaje.ProgramarReintento("HTTP 503", Ahora);

        Assert.Equal((EstadoMensajeSaliente.Fallido, 5), (mensaje.Estado, mensaje.Intentos));
        Assert.Throws<InvalidOperationException>(() => mensaje.MarcarEnviado("wamid", Ahora));
    }

    [Fact]
    public void Saliente_EnviadoGuardaLaCorrelacion_YLosEstadosDeEntrega()
    {
        var enviado = new MensajeSaliente(Guid.NewGuid(), "+57300", "Hola", Ahora, Guid.NewGuid());
        enviado.MarcarEnviado("wamid.9", Ahora);
        enviado.RegistrarEstadoExterno("delivered", null);
        enviado.RegistrarEstadoExterno("failed", "131047: Re-engagement message");

        Assert.Equal((EstadoMensajeSaliente.Enviado, "wamid.9", "failed", "131047: Re-engagement message"),
            (enviado.Estado, enviado.IdExterno, enviado.EstadoExterno, enviado.UltimoError));

        var permanente = new MensajeSaliente(Guid.NewGuid(), "+57300", "Hola", Ahora);
        permanente.MarcarFallido("HTTP 400: destinatario inválido");
        Assert.Equal(EstadoMensajeSaliente.Fallido, permanente.Estado);
    }
}

public class TelefonosWhatsAppTests
{
    [Theory]
    [InlineData("+573001112233", "+573001112233")]
    [InlineData(" +57 300 111-2233 ", "+573001112233")]
    [InlineData("+1 (555) 203.2563", "+15552032563")]
    public void Normaliza_AlFormatoE164(string telefono, string esperado) =>
        Assert.Equal(esperado, FlowRide.Domain.Identidad.TelefonosWhatsApp.Normalizar(telefono));

    [Theory]
    [InlineData("3001112233")]
    [InlineData("+57300")]
    [InlineData("+0573001112233")]
    [InlineData("<tu celular, p. ej. +573001112233>")]
    [InlineData(null)]
    public void RechazaLoQueNoEsE164(string? telefono) =>
        Assert.Throws<FlowRide.Domain.Transversal.FlowRideExcepcion>(() => FlowRide.Domain.Identidad.TelefonosWhatsApp.Normalizar(telefono));
}
