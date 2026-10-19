using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FlowRide.Application.Notificaciones.Entrada.Dto;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Infrastructure.Asignaciones;
using FlowRide.Infrastructure.Canal;
using FlowRide.Infrastructure.Notificaciones;
using FlowRide.IntegrationTests.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.IntegrationTests.Notificaciones;

/// <summary>
/// Notificaciones de punta a punta (RF-16, RF-17, RF-35, RF-43 a RF-45; ADR-0015, ADR-0016) con la API real, PostgreSQL
/// real y el proveedor de WhatsApp simulado: Outbox → worker de notificaciones → bandejas → worker del canal.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class NotificacionesApiTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    private WhatsAppSimuladoAdapter Proveedor => Api.Services.GetRequiredService<WhatsAppSimuladoAdapter>();

    private async Task<(int Eventos, int Correos)> NotificarAsync()
    {
        var resultado = await Api.Services.GetRequiredService<WorkerNotificaciones>().ProcesarAsync(CancellationToken.None);
        await Api.Services.GetRequiredService<WorkerCanalWhatsApp>().ProcesarAsync(CancellationToken.None);
        return resultado;
    }

    /// <summary>El usuario escribe por WhatsApp (webhook firmado): abre su ventana de 24 h.</summary>
    private async Task EscribirAsync(string telefono, string texto = "hola")
    {
        var cuerpo = JsonSerializer.Serialize(new
        {
            @object = "whatsapp_business_account",
            entry = new[]
            {
                new
                {
                    id = "WABA",
                    changes = new[]
                    {
                        new
                        {
                            field = "messages",
                            value = new
                            {
                                metadata = new { phone_number_id = FlowRideApiFactory.NumeroWhatsApp },
                                messages = new[]
                                {
                                    new
                                    {
                                        from = telefono.TrimStart('+'), id = $"wamid.{Guid.NewGuid():N}",
                                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                                        type = "text", text = new { body = texto }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        contenido.Headers.Add("X-Hub-Signature-256", FormatoWebhookMeta.Firmar(FlowRideApiFactory.SecretoAppWhatsApp, cuerpo));
        Assert.Equal(HttpStatusCode.OK, (await Api.CreateClient().PostAsync("/api/whatsapp/webhook", contenido)).StatusCode);
    }

    private List<string> EnviadosA(string telefono) => Proveedor.Enviados.Where(e => e.Telefono == telefono).Select(e => e.Texto).ToList();

    [Fact]
    public async Task ElConductorRecibeSuAsignacion_YElPasajeroSuConductorConPlantillaAprobada()
    {
        var telefonoConductor = Telefono();
        await ConductorListoAsync(telefono: telefonoConductor);
        await EscribirAsync(telefonoConductor);
        await NotificarAsync();
        var solicitudId = await SolicitudAsync();
        var asignacion = await AsignarAsync(solicitudId);

        var vuelta = await NotificarAsync();

        Assert.True(vuelta.Eventos >= 1);
        Assert.Contains(EnviadosA(telefonoConductor), t => t.StartsWith("Tienes un servicio nuevo", StringComparison.Ordinal));
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>(
            "SELECT count(*) FROM notif.outbox_mensaje WHERE tipo_evento = 'recordatorio.servicio' AND procesado_en IS NULL"));

        // El pasajero nunca escribió: sin plantilla aprobada se alerta; con la plantilla aprobada se le envía.
        var porOperador = await Operador.PutAsJsonAsync("/api/notificaciones/plantillas/solicitud.pasajero.conductor_asignado",
            new PlantillaPeticion { Nombre = "conductor_asignado", Idioma = "es_CO", Estado = "Aprobada" });
        var aprobada = await Administrador.PutAsJsonAsync("/api/notificaciones/plantillas/solicitud.pasajero.conductor_asignado",
            new PlantillaPeticion { Nombre = "conductor_asignado", Idioma = "es_CO", Estado = "Aprobada" });
        Assert.Equal(HttpStatusCode.Forbidden, porOperador.StatusCode);
        Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        await Operador.PostAsync($"/api/asignaciones/{asignacion.AsignacionId}/aceptar", null);
        await NotificarAsync();

        var plantilla = Assert.Single(Proveedor.Plantillas);
        Assert.Equal(("conductor_asignado", "es_CO", 3), (plantilla.Plantilla, plantilla.Idioma, plantilla.Parametros.Count));
        Assert.Equal(0L, await EjecutarEnSiloAsync<long>(
            "SELECT count(*) FROM notif.outbox_mensaje WHERE procesado_en IS NULL AND tipo_evento <> 'recordatorio.servicio'"));
        var plantillas = (await Operador.GetFromJsonAsync<List<PlantillaRespuesta>>("/api/notificaciones/plantillas"))!;
        Assert.Equal(11, plantillas.Count);
    }

    [Fact]
    public async Task AlertasDelPanel_SeListanSeMarcanYLlegaEnVivo()
    {
        await using (var silo = await Entorno.AbrirSiloAsync(Tenant))
        {
            silo.Contexto.Usuarios.Add(new Domain.Entidades.Usuario(Guid.NewGuid(), "Administradora", Telefono(), Domain.Identidad.RolesSistema.AdministradorId,
                DateTimeOffset.UtcNow, "jefa@operador.co"));
            await silo.Contexto.SaveChangesAsync();
        }
        await SolicitudAsync(urgente: true);
        await Api.Services.GetRequiredService<WorkerColaAsignacion>().ProcesarAsync(CancellationToken.None);
        await NotificarAsync();

        var alertas = (await Operador.GetFromJsonAsync<PaginaRespuesta<AlertaLectura>>("/api/alertas?soloNoLeidas=true"))!;
        var alerta = Assert.Single(alertas.Elementos);
        Assert.Equal(("alerta.solicitud.urgente_sin_candidatos", Domain.Notificaciones.SeveridadAlerta.Critica), (alerta.Tipo, alerta.Severidad));
        // Crítica: un correo a cada administrador con correo (aquí no hay SMTP: queda pendiente con su error).
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM notif.correo_saliente WHERE destinatario = 'jefa@operador.co'"));

        // En vivo (SSE): llega la alerta con su secuencia como id del evento.
        using var corte = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/alertas/en-vivo?desde=0");
        using var respuesta = await Operador.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, corte.Token);
        Assert.Equal("text/event-stream", respuesta.Content.Headers.ContentType!.MediaType);
        using var lector = new StreamReader(await respuesta.Content.ReadAsStreamAsync(corte.Token));
        var lineas = new List<string>();
        while (lineas.Count < 3 && await lector.ReadLineAsync(corte.Token) is { } linea)
            if (linea.Length > 0) lineas.Add(linea);
        Assert.Contains("event: alerta", lineas);
        Assert.Contains($"id: {alerta.Secuencia}", lineas);
        Assert.Contains(lineas, l => l.StartsWith("data: ", StringComparison.Ordinal) && l.Contains("urgente_sin_candidatos", StringComparison.Ordinal));

        var leida = await Operador.PostAsync($"/api/alertas/{alerta.Id}/leida", null);
        Assert.Equal(HttpStatusCode.OK, leida.StatusCode);
        Assert.Empty((await Operador.GetFromJsonAsync<PaginaRespuesta<AlertaLectura>>("/api/alertas?soloNoLeidas=true"))!.Elementos);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Api.CreateClient().GetAsync("/api/alertas")).StatusCode);
    }

    [Fact]
    public async Task DosReplicas_ProcesanCadaEventoUnaSolaVez()
    {
        const int solicitudes = 12;
        for (var i = 0; i < solicitudes; i++) await SolicitudAsync(Servicio.AddMinutes(i));
        await Api.Services.GetRequiredService<WorkerColaAsignacion>().ProcesarAsync(CancellationToken.None);

        await using var otraReplica = await ApiAsync();
        var workers = new[] { Api, otraReplica }.Select(api => api.Services.GetRequiredService<WorkerNotificaciones>()).ToArray();
        await Task.WhenAll(workers.SelectMany(worker => Enumerable.Range(0, 3).Select(_ => worker.ProcesarAsync(CancellationToken.None))));

        Assert.Equal(0L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM notif.outbox_mensaje WHERE procesado_en IS NULL"));
        Assert.Equal((long)solicitudes, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM notif.alerta"));
        Assert.Equal(0L, await EjecutarEnSiloAsync<long>(
            "SELECT count(*) FROM (SELECT event_id FROM notif.alerta GROUP BY event_id HAVING count(*) > 1) repetidas"));
    }
}

/// <summary>El envío de correo por SMTP contra un Mailpit real (ADR-0015).</summary>
[Trait("Category", "Integration")]
public class EnviadorCorreoSmtpTests : IAsyncLifetime
{
    private readonly DotNet.Testcontainers.Containers.IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.27.7")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(peticion => peticion.ForPort(8025).ForPath("/")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public async Task DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task EnviaElCorreoAlServidorSmtp()
    {
        var enviador = new EnviadorCorreoSmtp(new CorreoOptions { Host = _mailpit.Hostname, Puerto = _mailpit.GetMappedPublicPort(1025) });

        await enviador.EnviarAsync("admin@operador.co", "[FlowRide] Turno abandonado", "Detalle de la alerta", CancellationToken.None);

        using var http = new HttpClient();
        var mensajes = await http.GetStringAsync($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}/api/v1/messages");
        Assert.Contains("[FlowRide] Turno abandonado", mensajes, StringComparison.Ordinal);
        Assert.Contains("admin@operador.co", mensajes, StringComparison.Ordinal);
    }
}
