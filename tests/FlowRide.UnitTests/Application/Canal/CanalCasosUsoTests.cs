using FlowRide.Application.Canal.Bot;
using FlowRide.Application.Canal.CasosUso;
using FlowRide.Application.Canal.Entrada.Adaptadores;
using FlowRide.Application.Canal.Entrada.Dto;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.GestionCanal;
using FlowRide.Domain.Canal.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Identidad;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Canal;

internal sealed class CanalEnMemoria : IMensajeEntranteRepositorio, IMensajeSalienteRepositorio, IConversacionRepositorio
{
    public Dictionary<Guid, MensajeEntrante> Entrantes { get; } = [];
    public Dictionary<Guid, MensajeSaliente> Salientes { get; } = [];
    public Dictionary<string, Conversacion> Conversaciones { get; } = new(StringComparer.Ordinal);

    public Task CrearAsync(MensajeEntrante entidad, CancellationToken ct)
    {
        Entrantes.Add(entidad.Id, entidad);
        return Task.CompletedTask;
    }

    public Task CrearAsync(MensajeSaliente entidad, CancellationToken ct)
    {
        Salientes.Add(entidad.Id, entidad);
        return Task.CompletedTask;
    }

    public Task CrearAsync(Conversacion entidad, CancellationToken ct)
    {
        Conversaciones.Add(entidad.Telefono, entidad);
        return Task.CompletedTask;
    }

    Task<MensajeEntrante?> IMensajeEntranteRepositorio.ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Entrantes.GetValueOrDefault(id));

    public Task<bool> ExisteAsync(string idExterno, CancellationToken ct) => Task.FromResult(Entrantes.Values.Any(m => m.IdExterno == idExterno));

    public Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Entrantes.Values.Where(m => m.Estado == EstadoMensajeEntrante.Pendiente).OrderBy(m => m.RecibidoEn)
            .Select(m => m.Id).Take(maximo).ToList());

    Task<MensajeSaliente?> IMensajeSalienteRepositorio.ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Salientes.GetValueOrDefault(id));

    public Task<MensajeSaliente?> ConsultarPorIdExternoAsync(string idExterno, CancellationToken ct) =>
        Task.FromResult(Salientes.Values.FirstOrDefault(m => m.IdExterno == idExterno));

    public Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Salientes.Values.Where(m => m.ListoParaEnviar(ahora)).Select(m => m.Id).Take(maximo).ToList());

    public Task<Conversacion?> ConsultarPorTelefonoAsync(string telefono, CancellationToken ct) => Task.FromResult(Conversaciones.GetValueOrDefault(telefono));

    public Task<DateTimeOffset?> UltimoRecibidoAsync(string telefono, CancellationToken ct) =>
        Task.FromResult(Entrantes.Values.Where(m => m.Remitente == telefono).Select(m => (DateTimeOffset?)m.RecibidoEn).Max());
}

internal sealed class GatewayDePrueba : IWhatsAppGateway
{
    public bool Firma { get; set; } = true;
    public WebhookTraducido? Webhook { get; set; } = new([], [], 0);
    public Queue<ResultadoEnvio> Resultados { get; } = new();
    public List<(string Telefono, string Texto)> Enviados { get; } = [];

    public bool VerificarSuscripcion(string? modo, string? token) => token == "secreto";

    public bool FirmaValida(string cuerpo, string? firma) => Firma;

    public WebhookTraducido Traducir(string cuerpo) => Webhook ?? throw new FormatException("JSON inválido");

    public Task<ResultadoEnvio> EnviarTextoAsync(string telefono, string texto, CancellationToken ct)
    {
        Enviados.Add((telefono, texto));
        return Task.FromResult(Resultados.TryDequeue(out var resultado) ? resultado : ResultadoEnvio.Enviado($"wamid.{Enviados.Count}"));
    }

    public List<(string Telefono, string Plantilla, string Idioma, IReadOnlyList<string> Parametros)> Plantillas { get; } = [];

    public Task<ResultadoEnvio> EnviarPlantillaAsync(string telefono, string plantilla, string idioma, IReadOnlyList<string> parametros,
        CancellationToken ct)
    {
        Plantillas.Add((telefono, plantilla, idioma, parametros));
        return Task.FromResult(Resultados.TryDequeue(out var resultado) ? resultado : ResultadoEnvio.Enviado($"wamid.p{Plantillas.Count}"));
    }
}

public class CanalCasosUsoTests
{
    private static readonly DateTimeOffset Ahora = BotDePrueba.Ahora;
    private const string Telefono = "+573001112233";

    private readonly CanalEnMemoria _canal = new();
    private readonly UsuariosEnMemoria _usuarios = new();
    private readonly AuditoriaEnMemoria _auditoria = new();
    private readonly UnidadTrabajoEnMemoria _unidadTrabajo;
    private readonly BotDePrueba _bot = new();
    private readonly GatewayDePrueba _gateway = new();
    private readonly FlowRide.UnitTests.Application.Asignaciones.BandejaEnMemoria _bandeja = new();
    private readonly RelojFijo _reloj = new(Ahora);

    public CanalCasosUsoTests() => _unidadTrabajo = new UnidadTrabajoEnMemoria(_auditoria);

    private Pasajero PasajeroRegistrado(bool activo = true)
    {
        var pasajero = new Pasajero(Guid.NewGuid(), "Ana Ruiz", Telefono, RolesSistema.PasajeroId, Ahora, "E-1", Guid.NewGuid());
        if (!activo) pasajero.Desactivar();
        _usuarios.Usuarios[pasajero.Id] = pasajero;
        return pasajero;
    }

    private MensajeEntrante Entrante(string texto, string telefono = Telefono)
    {
        var mensaje = new MensajeEntrante(Guid.NewGuid(), $"wamid.{Guid.NewGuid():N}", telefono, MensajeRecibido.TipoTexto, texto, Ahora);
        _canal.Entrantes.Add(mensaje.Id, mensaje);
        return mensaje;
    }

    private ProcesarMensajeEntranteCasoUso Procesar() => new(_canal, _canal, _canal, _usuarios, _bot.Bot, _unidadTrabajo, _reloj);

    private string UltimaRespuesta => _canal.Salientes.Values.OrderBy(m => m.CreadoEn).Last().Contenido;

    [Fact]
    public async Task Procesar_ResponderAlRegistrado_YMarcarElMensaje()
    {
        var pasajero = PasajeroRegistrado();
        var mensaje = Entrante("hola");

        Assert.True(await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(mensaje.Id), CancellationToken.None));
        Assert.False(await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(mensaje.Id), CancellationToken.None));

        Assert.Equal((EstadoMensajeEntrante.Procesado, (Guid?)pasajero.Id), (mensaje.Estado, mensaje.UsuarioId));
        var respuesta = Assert.Single(_canal.Salientes.Values);
        Assert.Equal((Telefono, (Guid?)mensaje.Id), (respuesta.Destinatario, respuesta.EnRespuestaA));
        Assert.Contains("Pedir transporte", respuesta.Contenido);
        Assert.Single(_canal.Conversaciones);
        Assert.Equal(1, _unidadTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task Procesar_NumeroDesconocidoOInactivo_NoAbreConversacion()
    {
        await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(Entrante("hola", "+570000000").Id), CancellationToken.None);
        Assert.Equal(TextosBot.NoRegistrado, UltimaRespuesta);

        PasajeroRegistrado(activo: false);
        await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(Entrante("hola").Id), CancellationToken.None);
        Assert.Equal(TextosBot.Inactivo, UltimaRespuesta);
        Assert.Empty(_canal.Conversaciones);
    }

    [Fact]
    public async Task Procesar_AccionQueFallaPorRegla_DescartaYRespondeElMotivo()
    {
        PasajeroRegistrado();
        _bot.Lecturas.Solicitudes.Add(new(Guid.NewGuid(), _usuarios.Usuarios.Keys.Single(), "Ana", null, null, null, "Hotel", "Aeropuerto",
            Ahora.AddMinutes(10), TimeSpan.FromMinutes(45), 1, false, false, CanalSolicitud.WhatsApp,
            EstadoSolicitud.Asignada, Ahora));
        _bot.Cancelar.Falla = new FlowRideExcepcion("La ventana de cancelación ya cerró.");
        foreach (var texto in new[] { "3", "1" })
            await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(Entrante(texto).Id), CancellationToken.None);
        var ultimo = Entrante("Ya no lo necesito");

        await Procesar().EjecutarAsync(new ProcesarMensajeEntranteDominio(ultimo.Id), CancellationToken.None);

        Assert.Equal("No se pudo completar: La ventana de cancelación ya cerró. " + TextosBot.AyudaMenu, UltimaRespuesta);
        Assert.Equal(1, _unidadTrabajo.Descartes);
        Assert.Equal(EstadoMensajeEntrante.Procesado, ultimo.Estado);
        Assert.Equal(PasoConversacion.Menu, _canal.Conversaciones[Telefono].Paso);
    }

    [Fact]
    public async Task Webhook_FirmaInvalida_Malformado_DuplicadosYEstados()
    {
        var casoUso = new RecibirWebhookCasoUso(_gateway, new RegistroEntrantes(_canal, _reloj), _canal, _unidadTrabajo);
        var enviado = new MensajeSaliente(Guid.NewGuid(), Telefono, "Hola", Ahora);
        enviado.MarcarEnviado("wamid.saliente", Ahora);
        _canal.Salientes.Add(enviado.Id, enviado);
        var mensaje = new MensajeRecibido("wamid.A", Telefono, MensajeRecibido.TipoTexto, "hola", Ahora);
        _gateway.Webhook = new WebhookTraducido([mensaje, mensaje, mensaje with { IdExterno = "wamid.B" }],
            [new EstadoEntrega("wamid.saliente", "read", null), new EstadoEntrega("wamid.otro", "sent", null)], 1);

        var primero = await casoUso.EjecutarAsync(new RecibirWebhookDominio("{}", "sha256=x"), CancellationToken.None);
        var reenvio = await casoUso.EjecutarAsync(new RecibirWebhookDominio("{}", "sha256=x"), CancellationToken.None);
        _gateway.Webhook = null;
        var malformado = await casoUso.EjecutarAsync(new RecibirWebhookDominio("no", "sha256=x"), CancellationToken.None);
        _gateway.Firma = false;
        var sinFirma = await casoUso.EjecutarAsync(new RecibirWebhookDominio("{}", null), CancellationToken.None);

        Assert.Equal(new ResultadoWebhook(true, true, 2, 1, 1, 1), primero);
        Assert.Equal((0, 3), (reenvio.Nuevos, reenvio.Duplicados));
        Assert.Equal(2, _canal.Entrantes.Count);
        Assert.Equal("read", enviado.EstadoExterno);
        Assert.Same(ResultadoWebhook.Malformado, malformado);
        Assert.Same(ResultadoWebhook.FirmaInvalida, sinFirma);
    }

    [Fact]
    public async Task Enviar_GuardaLaCorrelacion_ReintentaLosTransitorios_YNoLosPermanentes()
    {
        var casoUso = new EnviarMensajeSalienteCasoUso(_canal, _gateway, _bandeja, _unidadTrabajo, _reloj);
        MensajeSaliente Nuevo()
        {
            var m = new MensajeSaliente(Guid.NewGuid(), Telefono, "Hola", Ahora);
            _canal.Salientes.Add(m.Id, m);
            return m;
        }
        var ok = Nuevo();
        var transitorio = Nuevo();
        var permanente = Nuevo();

        Assert.Equal(TipoResultadoEnvio.Enviado, await casoUso.EjecutarAsync(new EnviarMensajeSalienteDominio(ok.Id), CancellationToken.None));
        _gateway.Resultados.Enqueue(ResultadoEnvio.Transitorio("HTTP 503"));
        Assert.Equal(TipoResultadoEnvio.ErrorTransitorio, await casoUso.EjecutarAsync(new EnviarMensajeSalienteDominio(transitorio.Id), CancellationToken.None));
        _gateway.Resultados.Enqueue(ResultadoEnvio.Permanente("HTTP 400"));
        Assert.Equal(TipoResultadoEnvio.ErrorPermanente, await casoUso.EjecutarAsync(new EnviarMensajeSalienteDominio(permanente.Id), CancellationToken.None));

        Assert.Equal((EstadoMensajeSaliente.Enviado, "wamid.1"), (ok.Estado, ok.IdExterno));
        Assert.Equal((EstadoMensajeSaliente.Pendiente, Ahora + MensajeSaliente.Esperas[0]), (transitorio.Estado, transitorio.ProximoIntento));
        Assert.Equal((EstadoMensajeSaliente.Fallido, "HTTP 400"), (permanente.Estado, permanente.UltimoError));
        // El transitorio todavía no está listo; uno ya enviado tampoco.
        Assert.Null(await casoUso.EjecutarAsync(new EnviarMensajeSalienteDominio(transitorio.Id), CancellationToken.None));
        Assert.Null(await casoUso.EjecutarAsync(new EnviarMensajeSalienteDominio(ok.Id), CancellationToken.None));
        Assert.Empty(await new ConsultarSalientesPendientesCasoUso(_canal, _reloj).EjecutarAsync(new ConsultarSalientesPendientesDominio(10),
            CancellationToken.None));
    }

    [Fact]
    public async Task SimularFallosYPendientes()
    {
        var simular = new SimularMensajeCasoUso(new RegistroEntrantes(_canal, _reloj), _unidadTrabajo, _reloj);
        Assert.True(await simular.EjecutarAsync(new SimularMensajeDominio("57 300 111 2233", "hola"), CancellationToken.None));

        var mensaje = Assert.Single(_canal.Entrantes.Values);
        Assert.Equal((Telefono, "hola"), (mensaje.Remitente, mensaje.Contenido));
        Assert.Equal([mensaje.Id], await new ConsultarEntrantesPendientesCasoUso(_canal).EjecutarAsync(new ConsultarEntrantesPendientesDominio(5),
            CancellationToken.None));

        var fallo = new RegistrarFalloEntranteCasoUso(_canal, _unidadTrabajo);
        for (var i = 1; i < MensajeEntrante.MaximoIntentos; i++)
            Assert.False(await fallo.EjecutarAsync(new RegistrarFalloEntranteDominio(mensaje.Id, "timeout"), CancellationToken.None));
        Assert.True(await fallo.EjecutarAsync(new RegistrarFalloEntranteDominio(mensaje.Id, "timeout"), CancellationToken.None));
        Assert.False(await fallo.EjecutarAsync(new RegistrarFalloEntranteDominio(Guid.NewGuid(), "x"), CancellationToken.None));
    }

    [Fact]
    public async Task AdaptadorDeEntrada_ValidaYTraduce()
    {
        var consultas = new ConsultasCanalDePrueba();
        var adaptador = new CanalWhatsAppAdaptadorEntrada(_gateway,
            new RecibirWebhookCasoUso(_gateway, new RegistroEntrantes(_canal, _reloj), _canal, _unidadTrabajo),
            new SimularMensajeCasoUso(new RegistroEntrantes(_canal, _reloj), _unidadTrabajo, _reloj),
            new ConsultarEntrantesPendientesCasoUso(_canal), Procesar(), new RegistrarFalloEntranteCasoUso(_canal, _unidadTrabajo),
            new ConsultarSalientesPendientesCasoUso(_canal, _reloj), new EnviarMensajeSalienteCasoUso(_canal, _gateway, _bandeja, _unidadTrabajo, _reloj),
            new ConsultarConversacionCanalCasoUso(consultas));

        Assert.True(adaptador.VerificarSuscripcion("subscribe", "secreto"));
        Assert.Equal(new WebhookRespuesta(true, true, 0, 0, 0, 0), await adaptador.RecibirWebhookAsync(null!, null, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.SimularMensajeAsync(new MensajeSimuladoPeticion { Telefono = Telefono }, CancellationToken.None));
        await adaptador.SimularMensajeAsync(new MensajeSimuladoPeticion { Telefono = Telefono, Texto = "hola" }, CancellationToken.None);
        PasajeroRegistrado();
        var pendiente = Assert.Single(await adaptador.EntrantesPendientesAsync(5, CancellationToken.None));
        Assert.True(await adaptador.ProcesarEntranteAsync(pendiente, CancellationToken.None));
        var saliente = Assert.Single(await adaptador.SalientesPendientesAsync(5, CancellationToken.None));
        Assert.Equal("Enviado", await adaptador.EnviarSalienteAsync(saliente, CancellationToken.None));
        Assert.False(await adaptador.RegistrarFalloEntranteAsync(pendiente, "x", CancellationToken.None));
        await adaptador.ConversacionAsync("573001112233", CancellationToken.None);
        Assert.Equal(Telefono, consultas.Telefono);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ConversacionAsync(" ", CancellationToken.None));
    }

    private sealed class ConsultasCanalDePrueba : IConsultasCanal
    {
        public string? Telefono { get; private set; }

        public Task<IReadOnlyList<MensajeCanalLectura>> ConversacionAsync(string telefono, int maximo, CancellationToken ct)
        {
            Telefono = telefono;
            return Task.FromResult<IReadOnlyList<MensajeCanalLectura>>([]);
        }
    }
}

public class EnvioDePlantillasTests
{
    private static readonly DateTimeOffset Ahora = BotDePrueba.Ahora;
    private readonly CanalEnMemoria _canal = new();
    private readonly GatewayDePrueba _gateway = new();
    private readonly FlowRide.UnitTests.Application.Asignaciones.BandejaEnMemoria _bandeja = new();
    private readonly UnidadTrabajoEnMemoria _unidadTrabajo = new(new AuditoriaEnMemoria());

    private EnviarMensajeSalienteCasoUso Enviar() => new(_canal, _gateway, _bandeja, _unidadTrabajo, new RelojFijo(Ahora));

    [Fact]
    public async Task ConPlantilla_EnviaLaPlantillaConSusParametros()
    {
        var mensaje = new MensajeSaliente(Guid.NewGuid(), "+573001112233", "Texto", Ahora)
            .ConPlantilla("viaje_iniciado", "es_CO", "[\"Luis\",\"ABC123\"]");
        _canal.Salientes[mensaje.Id] = mensaje;

        Assert.Equal(TipoResultadoEnvio.Enviado, await Enviar().EjecutarAsync(new EnviarMensajeSalienteDominio(mensaje.Id), CancellationToken.None));

        var plantilla = Assert.Single(_gateway.Plantillas);
        Assert.Equal(("viaje_iniciado", "es_CO"), (plantilla.Plantilla, plantilla.Idioma));
        Assert.Equal(["Luis", "ABC123"], plantilla.Parametros);
        Assert.Empty(_gateway.Enviados);
        Assert.Empty(_bandeja.Eventos);
    }

    [Fact]
    public async Task UnFalloPermanente_DejaElEventoParaAlertarAlPanel()
    {
        var mensaje = new MensajeSaliente(Guid.NewGuid(), "+573001112233", "Texto", Ahora);
        _canal.Salientes[mensaje.Id] = mensaje;
        _gateway.Resultados.Enqueue(ResultadoEnvio.Permanente("HTTP 400"));

        await Enviar().EjecutarAsync(new EnviarMensajeSalienteDominio(mensaje.Id), CancellationToken.None);

        Assert.Equal(EventosSalida.EnvioWhatsAppFallido, Assert.Single(_bandeja.Eventos).Tipo);
    }
}
