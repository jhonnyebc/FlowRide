using System.Text.Json;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Notificaciones.CasosUso;
using FlowRide.Application.Notificaciones.Catalogo;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.GestionNotificaciones;
using FlowRide.Domain.Notificaciones.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.UnitTests.Application.Asignaciones;
using FlowRide.UnitTests.Application.Canal;
using FlowRide.UnitTests.Application.Operacion;
using FlowRide.UnitTests.Application.Solicitudes;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Notificaciones;

internal sealed class NotificacionesEnMemoria : IAlertaRepositorio, ICorreoSalienteRepositorio, IPlantillaWhatsAppRepositorio, IConsultasAlertas
{
    public List<Alerta> Alertas { get; } = [];
    public List<CorreoSaliente> Correos { get; } = [];
    public Dictionary<string, PlantillaWhatsApp> Plantillas { get; } = new(StringComparer.Ordinal);

    public Task CrearAsync(Alerta entidad, CancellationToken ct)
    {
        Alertas.Add(entidad);
        return Task.CompletedTask;
    }

    public Task CrearAsync(CorreoSaliente entidad, CancellationToken ct)
    {
        Correos.Add(entidad);
        return Task.CompletedTask;
    }

    Task<Alerta?> IAlertaRepositorio.ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Alertas.SingleOrDefault(a => a.Id == id));

    Task<CorreoSaliente?> ICorreoSalienteRepositorio.ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Correos.SingleOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Correos.Where(c => c.ListoParaEnviar(ahora)).Select(c => c.Id).Take(maximo).ToList());

    public Task<PlantillaWhatsApp?> ConsultarAsync(string clave, CancellationToken ct) => Task.FromResult(Plantillas.GetValueOrDefault(clave));

    public Task<IReadOnlyList<PlantillaWhatsApp>> ConsultarTodasAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PlantillaWhatsApp>>(Plantillas.Values.ToList());

    public Task<PaginaResultado<AlertaLectura>> BuscarAsync(bool soloNoLeidas, Paginacion paginacion, CancellationToken ct)
    {
        var lista = Alertas.Where(a => !soloNoLeidas || a.LeidaEn is null).Select(Lectura).ToList();
        return Task.FromResult(new PaginaResultado<AlertaLectura>(lista, lista.Count, paginacion));
    }

    public Task<IReadOnlyList<AlertaLectura>> PosterioresAsync(long despuesDe, int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AlertaLectura>>(Alertas.Select(Lectura).Skip((int)despuesDe).Take(maximo).ToList());

    private AlertaLectura Lectura(Alerta a) =>
        new(a.Id, Alertas.IndexOf(a) + 1, a.Tipo, a.Severidad, a.Titulo, a.Detalle, a.CreadaEn, a.ReferenciaId, a.LeidaEn);
}

public class CatalogoMensajesTests
{
    private static readonly CatalogoMensajes Catalogo = CatalogoMensajes.Cargar();

    [Fact]
    public void ArmaElTextoYLosParametrosEnOrden()
    {
        var mensaje = Catalogo.Armar("solicitud.pasajero.conductor_asignado",
            new Dictionary<string, string> { ["placa"] = "ABC123", ["conductor"] = "Luis", ["fecha"] = "19/10 07:30" });

        Assert.Equal("Tu servicio del 19/10 07:30 ya tiene conductor: Luis, vehículo de placa ABC123.", mensaje.Texto);
        Assert.Equal(["19/10 07:30", "Luis", "ABC123"], mensaje.Parametros);
    }

    [Fact]
    public void LasAlertasTienenTituloYDetalle_YFaltanVariablesOClave_Falla()
    {
        var (titulo, detalle) = Catalogo.Armar("alerta.turno.abandonado",
            new Dictionary<string, string> { ["conductor"] = "Luis", ["motivo"] = "Emergencia", ["solicitudes"] = "2" }).TituloYDetalle();

        Assert.Equal("Turno abandonado", titulo);
        Assert.StartsWith("El conductor Luis abandonó su turno", detalle, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => Catalogo.Armar("alerta.turno.abandonado", new Dictionary<string, string>()));
        Assert.Throws<InvalidOperationException>(() => Catalogo.Armar("no.existe", new Dictionary<string, string>()));
        Assert.Throws<InvalidOperationException>(() => CatalogoMensajes.Cargar("en-US"));
    }

    [Fact]
    public void CadaVariableDeclaradaApareceEnSuTexto()
    {
        foreach (var clave in Catalogo.Claves)
        {
            var valores = Catalogo.Variables(clave).ToDictionary(v => v, v => $"<{v}>");
            var texto = Catalogo.Armar(clave, valores).Texto;
            Assert.All(valores.Values, valor => Assert.Contains(valor, texto, StringComparison.Ordinal));
            Assert.DoesNotContain("{", texto, StringComparison.Ordinal);
        }
    }
}

public class NotificacionesCasosUsoTests : CasosUsoOperacionBase
{
    private static readonly DateTimeOffset Servicio = Ahora.AddDays(1);
    private readonly SolicitudesEnMemoria _solicitudes;
    private readonly CanalEnMemoria _canal = new();
    private readonly NotificacionesEnMemoria _notificaciones = new();
    private readonly BandejaEnMemoria _bandeja = new();
    private readonly ParametrosEnMemoria _parametros = new();
    private readonly Pasajero _pasajero;
    private readonly Conductor _conductor;
    private readonly Vehiculo _vehiculo;
    private readonly SolicitudViaje _solicitud;
    private readonly Asignacion _asignacion;

    public NotificacionesCasosUsoTests()
    {
        _solicitudes = new SolicitudesEnMemoria(Pasajeros, Empresas);
        var empresa = Empresas.Registrar();
        _pasajero = new Pasajero(Guid.NewGuid(), "Ana Ruiz", "+573001112233", RolesSistema.PasajeroId, Ahora, "E-1", empresa.Id);
        Pasajeros.Elementos[_pasajero.Id] = _pasajero;
        Usuarios.Usuarios[_pasajero.Id] = _pasajero;
        _conductor = ConductorRegistrado("3004445566");
        Usuarios.Usuarios[_conductor.Id] = _conductor;
        _vehiculo = new Vehiculo(Guid.NewGuid(), "ABC123", "Toyota", "Hiace", 10, Vigente, Vigente);
        Vehiculos.Elementos[_vehiculo.Id] = _vehiculo;
        _solicitud = new SolicitudViaje(Guid.NewGuid(), _pasajero.Id, "Hotel Centro", "Aeropuerto", Servicio.Local(), TimeSpan.FromMinutes(45), 2,
            CanalSolicitud.WhatsApp, Ahora, empresaClienteId: empresa.Id);
        _solicitudes.Solicitudes[_solicitud.Id] = _solicitud;
        _asignacion = Asignar(_conductor);
    }

    private Asignacion Asignar(Conductor conductor)
    {
        var asignacion = new Asignacion(Guid.NewGuid(), _solicitud.Id, conductor.Id, _vehiculo.Id, _solicitud.Franja(), OrigenAsignacion.Manual, Ahora);
        Asignaciones.Todas[asignacion.Id] = asignacion;
        return asignacion;
    }

    /// <summary>El usuario escribió hace poco: la ventana de 24 h está abierta.</summary>
    private void AbrirVentana(Usuario usuario) =>
        _canal.Entrantes[Guid.NewGuid()] = new MensajeEntrante(Guid.NewGuid(), $"wamid.{Guid.NewGuid():N}", usuario.TelefonoWhatsApp, "texto", "hola",
            Ahora.AddHours(-1));

    private GatewayNotificaciones Gateway => new(CatalogoMensajes.Cargar(), Usuarios, _canal, _canal, _notificaciones, _notificaciones,
        _notificaciones, Reloj);

    private ProcesarEventoCasoUso Procesar() => new(Gateway, _solicitudes, Asignaciones, Conductores, Vehiculos, Usuarios, _canal,
        new ParametrosDominio(_parametros), new ZonaHorariaOperador(TimeZoneInfo.Utc), _bandeja, UnidadTrabajo, Reloj);

    private Task<bool> EventoAsync(string tipo, object contenido) => Procesar().EjecutarAsync(
        new ProcesarEventoDominio(Guid.NewGuid(), tipo, JsonSerializer.Serialize(contenido, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Ahora),
        CancellationToken.None);

    private List<MensajeSaliente> MensajesPara(Usuario usuario) =>
        _canal.Salientes.Values.Where(m => m.Destinatario == usuario.TelefonoWhatsApp).ToList();

    private Usuario Administrador(string? correo = "admin@a.co")
    {
        var admin = new Usuario(Guid.NewGuid(), "Admin", "+573009990000", RolesSistema.AdministradorId, Ahora, correo);
        Usuarios.Usuarios[admin.Id] = admin;
        return admin;
    }

    [Fact]
    public async Task AsignacionNueva_AvisaAlConductor_YProgramaElRecordatorio()
    {
        AbrirVentana(_conductor);

        Assert.True(await EventoAsync(EventosSalida.AsignacionCreada, new { asignacionId = _asignacion.Id }));

        var mensaje = Assert.Single(MensajesPara(_conductor));
        Assert.Equal(("asignacion.conductor.nueva", false), (mensaje.ClaveMensaje, mensaje.EsPlantilla));
        Assert.Contains("Hotel Centro → Aeropuerto, 2 pasajero(s)", mensaje.Contenido, StringComparison.Ordinal);
        var recordatorio = Assert.Single(_bandeja.Programados);
        Assert.Equal((EventosSalida.RecordatorioServicio, Servicio.AddMinutes(-60)), (recordatorio.Tipo, recordatorio.Cuando));
        Assert.Equal(1, UnidadTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task FueraDeLaVentana_UsaLaPlantillaAprobada()
    {
        _notificaciones.Plantillas["asignacion.conductor.nueva"] =
            new PlantillaWhatsApp("asignacion.conductor.nueva", "nueva_asignacion", "es_CO", EstadoPlantilla.Aprobada);

        await EventoAsync(EventosSalida.AsignacionCreada, new { asignacionId = _asignacion.Id });

        var mensaje = Assert.Single(MensajesPara(_conductor));
        Assert.Equal(("nueva_asignacion", "es_CO"), (mensaje.Plantilla, mensaje.IdiomaPlantilla));
        Assert.Equal(4, JsonSerializer.Deserialize<List<string>>(mensaje.ParametrosPlantilla!)!.Count);
        Assert.Empty(_notificaciones.Alertas);
    }

    [Fact]
    public async Task FueraDeLaVentanaSinPlantillaAprobada_AlertaYEscribeALosAdministradores()
    {
        Administrador();
        Administrador(correo: null);
        _notificaciones.Plantillas["asignacion.conductor.nueva"] = new PlantillaWhatsApp("asignacion.conductor.nueva", "x", "es_CO");

        await EventoAsync(EventosSalida.AsignacionCreada, new { asignacionId = _asignacion.Id });

        Assert.Empty(MensajesPara(_conductor));
        var alerta = Assert.Single(_notificaciones.Alertas);
        Assert.Equal((GatewayNotificaciones.ClaveFalloWhatsApp, SeveridadAlerta.Critica, (Guid?)_conductor.Id),
            (alerta.Tipo, alerta.Severidad, alerta.ReferenciaId));
        Assert.Contains("24 h", alerta.Detalle, StringComparison.Ordinal);
        var correo = Assert.Single(_notificaciones.Correos);
        Assert.Equal(("admin@a.co", alerta.Id), (correo.Destinatario, correo.AlertaId));
        Assert.StartsWith("[FlowRide] ", correo.Asunto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reasignacion_AvisaAlAnterior_AlNuevo_YAlPasajero()
    {
        var otro = ConductorRegistrado("3007778899");
        Usuarios.Usuarios[otro.Id] = otro;
        var nueva = Asignar(otro);
        foreach (var usuario in new Usuario[] { _conductor, otro, _pasajero }) AbrirVentana(usuario);

        await EventoAsync(EventosSalida.AsignacionReasignada,
            new { anterior = new { asignacionId = _asignacion.Id }, nueva = new { asignacionId = nueva.Id }, motivo = "Conductor enfermo" });

        Assert.Equal("asignacion.conductor.retirada", Assert.Single(MensajesPara(_conductor)).ClaveMensaje);
        Assert.Contains("Conductor enfermo", MensajesPara(_conductor)[0].Contenido, StringComparison.Ordinal);
        Assert.Equal("asignacion.conductor.nueva", Assert.Single(MensajesPara(otro)).ClaveMensaje);
        Assert.Equal("solicitud.pasajero.cambio_conductor", Assert.Single(MensajesPara(_pasajero)).ClaveMensaje);
        Assert.Single(_bandeja.Programados);
    }

    [Fact]
    public async Task Cancelaciones_AvisanAlConductor_YAlPasajeroSiNoFueEl()
    {
        AbrirVentana(_conductor);
        AbrirVentana(_pasajero);

        await EventoAsync(EventosSalida.AsignacionCancelada, new { asignacionId = _asignacion.Id, motivo = "Vuelo cancelado" });
        Assert.False(await EventoAsync(EventosSalida.SolicitudCancelada,
            new { solicitudId = _solicitud.Id, motivo = "Ya no viajo", porPasajero = true }));
        await EventoAsync(EventosSalida.SolicitudCancelada, new { solicitudId = _solicitud.Id, motivo = "Vuelo cancelado", porPasajero = false });

        Assert.Equal("asignacion.conductor.cancelada", Assert.Single(MensajesPara(_conductor)).ClaveMensaje);
        var alPasajero = Assert.Single(MensajesPara(_pasajero));
        Assert.Equal("solicitud.pasajero.cancelada", alPasajero.ClaveMensaje);
        Assert.Contains("Vuelo cancelado", alPasajero.Contenido, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlPasajero_ConductorAsignado_InicioYFin()
    {
        AbrirVentana(_pasajero);

        await EventoAsync(EventosSalida.AsignacionAceptada, new { asignacionId = _asignacion.Id });
        await EventoAsync(EventosSalida.ViajeIniciado, new { asignacionId = _asignacion.Id });
        await EventoAsync(EventosSalida.ViajeFinalizado, new { asignacionId = _asignacion.Id });

        var mensajes = MensajesPara(_pasajero).Select(m => m.ClaveMensaje).ToList();
        Assert.Equal(["solicitud.pasajero.conductor_asignado", "viaje.pasajero.iniciado", "viaje.pasajero.finalizado"], mensajes);
        Assert.Contains("placa ABC123", MensajesPara(_pasajero)[0].Contenido, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recordatorio_SoloSiLaAsignacionSigueVigente()
    {
        AbrirVentana(_pasajero);
        AbrirVentana(_conductor);

        Assert.True(await EventoAsync(EventosSalida.RecordatorioServicio, new { asignacionId = _asignacion.Id }));
        Assert.Equal("recordatorio.pasajero", Assert.Single(MensajesPara(_pasajero)).ClaveMensaje);
        Assert.Equal("recordatorio.conductor", Assert.Single(MensajesPara(_conductor)).ClaveMensaje);

        _asignacion.Cancelar("Solicitud cancelada");
        Assert.False(await EventoAsync(EventosSalida.RecordatorioServicio, new { asignacionId = _asignacion.Id }));
        Assert.False(await EventoAsync(EventosSalida.RecordatorioServicio, new { asignacionId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task AlertasDelPanel_ConSuSeveridad()
    {
        Administrador();
        AbrirVentana(_pasajero);

        await EventoAsync(EventosSalida.SolicitudSinCandidatos, new { solicitudId = _solicitud.Id, motivo = "Sin conductores", urgente = false });
        await EventoAsync(EventosSalida.SolicitudSinCandidatos, new { solicitudId = _solicitud.Id, motivo = "Sin conductores", urgente = true });
        await EventoAsync(EventosSalida.TurnoAbandonado, new { conductorId = _conductor.Id, solicitudes = new[] { _solicitud.Id }, motivo = "Emergencia" });
        await EventoAsync(EventosSalida.AsignacionRechazada, new { asignacionId = _asignacion.Id, vencida = true });
        Assert.False(await EventoAsync(EventosSalida.AsignacionRechazada, new { asignacionId = _asignacion.Id, vencida = false }));
        await EventoAsync(EventosSalida.IncidenteReportado, new
        {
            viajeId = Guid.NewGuid(),
            solicitudId = _solicitud.Id,
            conductorId = _conductor.Id,
            tipo = "Retraso",
            descripcion = "Trancón"
        });
        await EventoAsync(EventosSalida.IncidenteReportado, new
        {
            viajeId = Guid.NewGuid(),
            solicitudId = _solicitud.Id,
            conductorId = _conductor.Id,
            tipo = "Accidente",
            descripcion = "Choque leve"
        });

        Assert.Equal(
        [
            ("alerta.solicitud.sin_candidatos", SeveridadAlerta.Advertencia),
            ("alerta.solicitud.urgente_sin_candidatos", SeveridadAlerta.Critica),
            ("alerta.turno.abandonado", SeveridadAlerta.Critica),
            ("alerta.asignacion.vencida", SeveridadAlerta.Advertencia),
            ("alerta.viaje.incidente", SeveridadAlerta.Advertencia),
            ("alerta.viaje.incidente", SeveridadAlerta.Critica)
        ], _notificaciones.Alertas.Select(a => (a.Tipo, a.Severidad)));
        Assert.Equal(3, _notificaciones.Correos.Count);
        Assert.Contains("1 solicitud(es)", _notificaciones.Alertas[2].Detalle, StringComparison.Ordinal);
        Assert.Equal("viaje.pasajero.retraso", Assert.Single(MensajesPara(_pasajero)).ClaveMensaje);
    }

    [Fact]
    public async Task EnvioFallido_AlertaConElNombreYElError_YLosTiposDesconocidosNoHacenNada()
    {
        var fallido = new MensajeSaliente(Guid.NewGuid(), _pasajero.TelefonoWhatsApp, "Hola", Ahora).ComoNotificacion("viaje.pasajero.iniciado", Guid.NewGuid());
        fallido.MarcarFallido("HTTP 400: (#131030) Recipient not in allowed list");
        _canal.Salientes[fallido.Id] = fallido;

        await EventoAsync(EventosSalida.EnvioWhatsAppFallido, new { mensajeId = fallido.Id });
        Assert.False(await EventoAsync("evento.desconocido", new { }));

        var alerta = Assert.Single(_notificaciones.Alertas);
        Assert.Contains("Ana Ruiz", alerta.Detalle, StringComparison.Ordinal);
        Assert.Contains("131030", alerta.Detalle, StringComparison.Ordinal);
        Assert.Equal(1, UnidadTrabajo.Confirmaciones);
    }

    [Fact]
    public async Task UsuarioInactivoOAdministrador_NoRecibenWhatsApp()
    {
        _conductor.Desactivar();
        Assert.False(await Gateway.NotificarAsync(_conductor.Id, "viaje.pasajero.finalizado", new Dictionary<string, string>(), Guid.NewGuid(),
            CancellationToken.None));
        Assert.False(await Gateway.NotificarAsync(Administrador().Id, "viaje.pasajero.finalizado", new Dictionary<string, string>(), Guid.NewGuid(),
            CancellationToken.None));
        Assert.Empty(_canal.Salientes);
    }

    private sealed class EnviadorDePrueba : IEnviadorCorreo
    {
        public List<(string Para, string Asunto)> Enviados { get; } = [];
        public bool Falla { get; set; }

        public Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct)
        {
            if (Falla) throw new InvalidOperationException("SMTP caído");
            Enviados.Add((destinatario, asunto));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Correos_SeEnvian_YLosFallosSeReintentan()
    {
        var enviador = new EnviadorDePrueba();
        var ok = new CorreoSaliente(Guid.NewGuid(), "a@a.co", "Asunto", "Cuerpo", Ahora);
        var falla = new CorreoSaliente(Guid.NewGuid(), "b@a.co", "Asunto", "Cuerpo", Ahora);
        _notificaciones.Correos.AddRange([ok, falla]);
        var casoUso = new EnviarCorreoCasoUso(_notificaciones, enviador, UnidadTrabajo, Reloj);

        Assert.Equal(2, (await new ConsultarCorreosPendientesCasoUso(_notificaciones, Reloj)
            .EjecutarAsync(new ConsultarCorreosPendientesDominio(10), CancellationToken.None)).Count);
        Assert.True(await casoUso.EjecutarAsync(new EnviarCorreoDominio(ok.Id), CancellationToken.None));
        enviador.Falla = true;
        Assert.False(await casoUso.EjecutarAsync(new EnviarCorreoDominio(falla.Id), CancellationToken.None));
        Assert.False(await casoUso.EjecutarAsync(new EnviarCorreoDominio(ok.Id), CancellationToken.None));

        Assert.Equal(("a@a.co", EstadoCorreo.Enviado), (Assert.Single(enviador.Enviados).Para, ok.Estado));
        Assert.Equal((EstadoCorreo.Pendiente, 1, "InvalidOperationException: SMTP caído"), (falla.Estado, falla.Intentos, falla.UltimoError));
    }

    [Fact]
    public async Task AlertasYPlantillas_DesdeElPanel()
    {
        var alerta = await Gateway.AlertarAsync("alerta.turno.abandonado", SeveridadAlerta.Info,
            new Dictionary<string, string> { ["conductor"] = "Luis", ["motivo"] = "x", ["solicitudes"] = "0" }, null, null, CancellationToken.None);
        var actor = Guid.NewGuid();

        var leida = await new MarcarAlertaLeidaCasoUso(_notificaciones, UnidadTrabajo, Reloj)
            .EjecutarAsync(new MarcarAlertaLeidaDominio(alerta.Id, actor), CancellationToken.None);
        Assert.Equal((Ahora, (Guid?)actor), (leida.LeidaEn, leida.LeidaPorId));
        Assert.Empty((await new BuscarAlertasCasoUso(_notificaciones).EjecutarAsync(new BuscarAlertasDominio(true, Paginacion.PorDefecto),
            CancellationToken.None)).Elementos);
        Assert.Single(await new AlertasPosterioresCasoUso(_notificaciones).EjecutarAsync(new AlertasPosterioresDominio(0, 10), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new MarcarAlertaLeidaCasoUso(_notificaciones, UnidadTrabajo, Reloj)
            .EjecutarAsync(new MarcarAlertaLeidaDominio(Guid.NewGuid(), actor), CancellationToken.None));

        _notificaciones.Plantillas["viaje.pasajero.iniciado"] = new PlantillaWhatsApp("viaje.pasajero.iniciado", "viaje_pasajero_iniciado", "es_CO");
        var plantilla = await new ActualizarPlantillaCasoUso(_notificaciones, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ActualizarPlantillaDominio("viaje.pasajero.iniciado", "viaje_iniciado_v2", "es", EstadoPlantilla.Aprobada, actor),
                CancellationToken.None);
        Assert.Equal(("viaje_iniciado_v2", "es", true, (Guid?)actor), (plantilla.Nombre, plantilla.Idioma, plantilla.Utilizable, plantilla.ModificadaPorId));
        Assert.Equal("plantilla_whatsapp.actualizar", UltimaAccion);
        Assert.Single(await new ConsultarPlantillasCasoUso(_notificaciones).EjecutarAsync(ConsultarPlantillasDominio.Todas, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ActualizarPlantillaCasoUso(_notificaciones, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ActualizarPlantillaDominio("no.existe", "x", "es", EstadoPlantilla.Aprobada, actor), CancellationToken.None));
    }
}
