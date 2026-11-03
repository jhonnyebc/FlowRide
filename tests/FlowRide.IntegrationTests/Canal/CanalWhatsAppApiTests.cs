using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Canal.Entrada.Dto;
using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Infrastructure.Canal;
using FlowRide.IntegrationTests.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.IntegrationTests.Canal;

/// <summary>
/// Canal WhatsApp de punta a punta (RF-14, RF-15, RF-18; ADR-0008; PoC 4) con la API real, PostgreSQL real y el proveedor
/// simulado: webhooks firmados con el formato de Meta, worker del canal y respuestas en la bandeja de salida.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class CanalWhatsAppApiTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private int _secuencia;

    private WhatsAppSimuladoAdapter Proveedor => Api.Services.GetRequiredService<WhatsAppSimuladoAdapter>();

    private Task<(int Procesados, int Enviados)> CanalAsync() =>
        Api.Services.GetRequiredService<WorkerCanalWhatsApp>().ProcesarAsync(CancellationToken.None);

    private static string Payload(string telefono, string texto, string? id = null, string numero = FlowRideApiFactory.NumeroWhatsApp) =>
        JsonSerializer.Serialize(new
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
                                messaging_product = "whatsapp",
                                metadata = new { display_phone_number = "15550000000", phone_number_id = numero },
                                messages = new[]
                                {
                                    new
                                    {
                                        from = telefono.TrimStart('+'), id = id ?? $"wamid.{Guid.NewGuid():N}",
                                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), type = "text",
                                        text = new { body = texto }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

    private async Task<HttpResponseMessage> WebhookAsync(string cuerpo, string? firma = "")
    {
        var cliente = Api.CreateClient();
        using var contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        if (firma is not null)
            contenido.Headers.Add("X-Hub-Signature-256", firma == "" ? FormatoWebhookMeta.Firmar(FlowRideApiFactory.SecretoAppWhatsApp, cuerpo) : firma);
        return await cliente.PostAsync("/api/whatsapp/webhook", contenido);
    }

    /// <summary>El usuario escribe un mensaje; el worker lo procesa y "envía" la respuesta. Devuelve la respuesta del bot.</summary>
    private async Task<string> EscribirAsync(string telefono, string texto)
    {
        var respuesta = await WebhookAsync(Payload(telefono, texto));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await CanalAsync();
        var enviados = Proveedor.Enviados.Where(e => e.Telefono == telefono).ToList();
        Assert.True(enviados.Count > _secuencia, $"El bot no respondió a «{texto}».");
        _secuencia = enviados.Count;
        return enviados[^1].Texto;
    }

    private async Task<string> PasajeroAsync()
    {
        var telefono = Telefono();
        var respuesta = await Administrador.PostAsJsonAsync("/api/pasajeros", new PasajeroPeticion
        {
            Nombre = "Marta Gómez",
            TelefonoWhatsApp = telefono,
            EmpresaClienteId = EmpresaId,
            CodigoInterno = $"E-{Guid.NewGuid():N}"[..12]
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return telefono;
    }

    [Fact]
    public async Task VerificacionDelWebhook_YFirma()
    {
        var cliente = Api.CreateClient();
        var verificado = await cliente.GetAsync(
            $"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={FlowRideApiFactory.TokenVerificacionWhatsApp}&hub.challenge=12345");
        var tokenMalo = await cliente.GetAsync("/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=otro&hub.challenge=12345");
        var sinFirma = await WebhookAsync(Payload("+573000000000", "hola"), firma: null);
        var firmaFalsa = await WebhookAsync(Payload("+573000000000", "hola"), "sha256=" + new string('0', 64));
        var malformado = await WebhookAsync("{\"no\":\"es meta\"}");
        var simulador = await cliente.PostAsJsonAsync("/api/whatsapp/simulado/mensajes", new MensajeSimuladoPeticion { Telefono = "+57300", Texto = "hola" });

        Assert.Equal(("12345", HttpStatusCode.OK), (await verificado.Content.ReadAsStringAsync(), verificado.StatusCode));
        Assert.Equal(HttpStatusCode.Forbidden, tokenMalo.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, sinFirma.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, firmaFalsa.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformado.StatusCode);
        // El simulador solo existe en desarrollo con el proveedor simulado: en las pruebas no hay ruta.
        Assert.Equal(HttpStatusCode.NotFound, simulador.StatusCode);
        Assert.Equal(0L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM canal.mensaje_entrante"));
    }

    [Fact]
    public async Task PoC4_UnMensajeRepetidoNoSeProcesaDosVeces_YOtroNumeroSeIgnora()
    {
        var telefono = await PasajeroAsync();
        var payload = Payload(telefono, "hola", "wamid.repetido");

        var primero = await (await WebhookAsync(payload)).Content.ReadFromJsonAsync<WebhookRespuesta>();
        var repetido = await (await WebhookAsync(payload)).Content.ReadFromJsonAsync<WebhookRespuesta>();
        var otroNumero = await (await WebhookAsync(Payload(telefono, "hola", numero: "999"))).Content.ReadFromJsonAsync<WebhookRespuesta>();
        var (procesados, enviados) = await CanalAsync();
        await CanalAsync();

        Assert.Equal((1, 0), (primero!.Nuevos, primero.Duplicados));
        Assert.Equal((0, 1), (repetido!.Nuevos, repetido.Duplicados));
        Assert.Equal((0, 1), (otroNumero!.Nuevos, otroNumero.Ignorados));
        Assert.Equal((1, 1), (procesados, enviados));
        Assert.Single(Proveedor.Enviados, e => e.Telefono == telefono);
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM canal.mensaje_entrante"));
    }

    [Fact]
    public async Task Pasajero_PideTransporte_PorWhatsApp_YLoConsulta()
    {
        var telefono = await PasajeroAsync();
        var servicio = TimeZoneInfo.ConvertTime(Servicio, Bogota);

        Assert.Contains("1. Pedir transporte", await EscribirAsync(telefono, "Hola"));
        await EscribirAsync(telefono, "1");
        await EscribirAsync(telefono, "Campamento base");
        await EscribirAsync(telefono, "Mina La Esperanza");
        Assert.Contains("Cuántas personas", await EscribirAsync(telefono, servicio.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)));
        Assert.Contains("Pasajeros: 3", await EscribirAsync(telefono, "3"));
        Assert.StartsWith("Listo, registramos tu solicitud", await EscribirAsync(telefono, "1"));
        Assert.Contains("buscando conductor", await EscribirAsync(telefono, "2"));

        var solicitudes = (await Operador.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>("/api/solicitudes"))!.Elementos;
        var solicitud = Assert.Single(solicitudes, s => s.DireccionOrigen == "Campamento base");
        Assert.Equal(("WhatsApp", 3, Servicio), (solicitud.Canal, solicitud.CantidadPasajeros, solicitud.FechaHoraServicio));

        // El panel ve la conversación; en la base el teléfono está cifrado.
        var conversacion = (await Operador.GetFromJsonAsync<List<MensajeCanalLectura>>($"/api/whatsapp/conversaciones/{Uri.EscapeDataString(telefono)}"))!;
        Assert.Equal(16, conversacion.Count);
        Assert.Equal(("entrante", "Hola"), (conversacion[0].Direccion, conversacion[0].Contenido));
        Assert.All(conversacion.Where(m => m.Direccion == "saliente"), m => Assert.Equal("Enviado", m.Estado));
        Assert.Equal(0L, await EjecutarEnSiloAsync<long>($"SELECT count(*) FROM canal.conversacion WHERE telefono = '{telefono}'"));
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM canal.conversacion"));
    }

    [Fact]
    public async Task Conductor_AceptaEIniciaPorWhatsApp_YLosEstadosDeEntregaSeCorrelacionan()
    {
        var telefono = Telefono();
        var conductor = await ConductorListoAsync(telefono: telefono);
        var solicitudId = await SolicitudAsync();
        var asignacion = await AsignarAsync(solicitudId);
        Assert.Equal(conductor.ConductorId, asignacion.ConductorId);

        Assert.Contains("Responder asignaciones", await EscribirAsync(telefono, "menu"));
        Assert.Contains("Campamento → Mina", await EscribirAsync(telefono, "1"));
        await EscribirAsync(telefono, "1");
        Assert.Contains("Aceptaste", await EscribirAsync(telefono, "1"));
        await EscribirAsync(telefono, "2");
        Assert.Contains("Viaje iniciado", await EscribirAsync(telefono, "1"));

        var viaje = Assert.Single((await Operador.GetFromJsonAsync<PaginaRespuesta<ViajeLectura>>($"/api/viajes?conductorId={conductor.ConductorId}"))!
            .Elementos);
        Assert.Equal(EstadoViaje.EnCurso, viaje.Estado);
        var estados = (await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?solicitudId={solicitudId}"))!.Elementos;
        Assert.Equal(EstadoAsignacion.EnCurso, Assert.Single(estados).Estado);
        var traza = (await Operador.GetFromJsonAsync<TrazabilidadLectura>($"/api/solicitudes/{solicitudId}/trazabilidad"))!;
        Assert.Equal(conductor.ConductorId, Assert.Single(traza.Eventos, e => e.Tipo == "asignacion.aceptar").ActorId);

        // Meta reporta la entrega de la última respuesta: queda correlacionada por su id externo.
        var ultimo = Proveedor.Enviados.Last(e => e.Telefono == telefono).IdExterno;
        var estado = JsonSerializer.Serialize(new
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
                                statuses = new[] { new { id = ultimo, status = "read", timestamp = "1760000000", recipient_id = telefono.TrimStart('+') } }
                            }
                        }
                    }
                }
            }
        });
        var actualizado = await (await WebhookAsync(estado)).Content.ReadFromJsonAsync<WebhookRespuesta>();
        Assert.Equal(1, actualizado!.EstadosActualizados);
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>($"SELECT count(*) FROM canal.mensaje_saliente WHERE id_externo = '{ultimo}' AND estado_externo = 'read'"));
    }

    [Fact]
    public async Task NumeroNoRegistrado_RecibeAviso_YUnErrorDelProveedorSeReintenta()
    {
        var desconocido = Telefono();
        Proveedor.FallarProximos.Enqueue(ResultadoEnvio.Transitorio("HTTP 503"));

        await WebhookAsync(Payload(desconocido, "hola"));
        var (procesados, enviados) = await CanalAsync();

        Assert.Equal((1, 1), (procesados, enviados));
        Assert.DoesNotContain(Proveedor.Enviados, e => e.Telefono == desconocido);
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>(
            "SELECT count(*) FROM canal.mensaje_saliente WHERE estado = 'Pendiente' AND intentos = 1 AND ultimo_error = 'HTTP 503'"));

        await EjecutarEnSiloAsync<int>("UPDATE canal.mensaje_saliente SET proximo_intento = now() - interval '1 minute' RETURNING 1");
        await CanalAsync();
        Assert.Contains("no está registrado", Assert.Single(Proveedor.Enviados, e => e.Telefono == desconocido).Texto);
    }
}
