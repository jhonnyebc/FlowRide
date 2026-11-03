using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Parametros.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Parametros;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.IntegrationTests.Solicitudes;

/// <summary>
/// Solicitudes e itinerarios de punta a punta (RF-08, RF-09, RF-29 a RF-32; PAS-SEG-01, PAS-AUD-02): API real, PostgreSQL
/// real y tokens de un emisor de prueba.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class SolicitudesApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static readonly DateTimeOffset Manana = DateTimeOffset.UtcNow.Date.AddDays(2).ToUniversalTime().AddHours(13);

    private readonly string _tenant = $"sol-{Guid.NewGuid():N}"[..14];
    private EntornoAprovisionamiento _entorno = null!;
    private FlowRideApiFactory _api = null!;
    private HttpClient _administrador = null!;
    private HttpClient _operador = null!;
    private Guid _empresaId;
    private Guid _pasajeroId;

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await _entorno.AprovisionarAsync(_tenant);
        _api = new FlowRideApiFactory(await _entorno.CadenaDelSiloAsync(_tenant), _tenant)
        {
            ConTokensDePrueba = true,
            Proveedor = new ProveedorIdentidadDePrueba(),
            Ajustes = await _entorno.AjustesDelStackAsync(_tenant)
        };
        _administrador = await ClienteAsync(RolesSistema.AdministradorId);
        _operador = await ClienteAsync(RolesSistema.OperadorLogisticoId);

        _empresaId = (await (await _administrador.PostAsJsonAsync("/api/empresas-cliente",
                new EmpresaClientePeticion { Nombre = "Minera del Sur", Nit = $"nit-{Guid.NewGuid():N}"[..14], CorreoContacto = "ops@minera.co" }))
            .Content.ReadFromJsonAsync<EmpresaClienteRespuesta>())!.Id;
        _pasajeroId = await PasajeroAsync();
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _entorno.DisposeAsync();
    }

    private async Task<HttpClient> ClienteAsync(Guid rolId)
    {
        var sub = $"sub-{Guid.NewGuid():N}";
        await using (var silo = await _entorno.AbrirSiloAsync(_tenant))
        {
            silo.Contexto.Usuarios.Add(new Usuario(Guid.NewGuid(), "Usuario del panel", $"+1{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
                rolId, DateTimeOffset.UtcNow, null, sub));
            await silo.Contexto.SaveChangesAsync();
        }
        var cliente = _api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokensDePrueba.Crear(sub, _tenant));
        return cliente;
    }

    private async Task<Guid> PasajeroAsync() =>
        (await (await _administrador.PostAsJsonAsync("/api/pasajeros", new PasajeroPeticion
        {
            Nombre = "Ana Gómez",
            TelefonoWhatsApp = $"+57{Random.Shared.NextInt64(3_000_000_000, 3_999_999_999)}",
            EmpresaClienteId = _empresaId,
            CodigoInterno = $"E-{Guid.NewGuid():N}"[..10]
        })).Content.ReadFromJsonAsync<PasajeroRespuesta>())!.Id;

    private static SolicitudPeticion Peticion(Guid pasajeroId, DateTimeOffset servicio, bool personal = false, bool urgente = false) => new()
    {
        PasajeroId = pasajeroId,
        DireccionOrigen = "Hotel Central",
        DireccionDestino = "Aeropuerto",
        FechaHoraServicio = servicio.HoraLocal(),
        DuracionMinutos = 45,
        CantidadPasajeros = 1,
        EsPersonal = personal,
        Urgente = urgente
    };

    private async Task<SolicitudDetalleRespuesta> RegistrarAsync(SolicitudPeticion peticion, HttpClient? cliente = null)
    {
        var respuesta = await (cliente ?? _operador).PostAsJsonAsync("/api/solicitudes", peticion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<SolicitudDetalleRespuesta>())!;
    }

    [Fact]
    public async Task ElServicio_SeGuardaEnHoraLocalConElNombreDeLaZona()
    {
        var local = new DateTime(Manana.Year, Manana.Month, Manana.Day, 20, 30, 0);
        var conZona = await RegistrarAsync(Peticion(_pasajeroId, Manana) with { FechaHoraServicio = local, ZonaHoraria = "America/Bogota" });
        var conDesfase = await _operador.PostAsync("/api/solicitudes", JsonContent.Create(new
        {
            pasajeroId = await PasajeroAsync(),
            direccionOrigen = "Hotel",
            direccionDestino = "Aeropuerto",
            fechaHoraServicio = $"{local:yyyy-MM-ddTHH:mm:ss}-05:00",
            duracionMinutos = 45,
            cantidadPasajeros = 1
        }));
        var zonaInvalida = await _operador.PostAsJsonAsync("/api/solicitudes",
            Peticion(await PasajeroAsync(), Manana) with { FechaHoraServicio = local, ZonaHoraria = "-05:00" });

        var s = conZona.Solicitud;
        Assert.Equal((local, "America/Bogota"), (s.HoraLocalServicio, s.ZonaHoraria));
        Assert.Equal(new DateTimeOffset(local, TimeSpan.FromHours(-5)), s.FechaHoraServicio);
        Assert.Equal(HttpStatusCode.BadRequest, conDesfase.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zonaInvalida.StatusCode);
        await using var silo = await _entorno.AbrirSiloAsync(_tenant);
        var guardada = await silo.Contexto.Solicitudes.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal((local, "America/Bogota", DateTimeKind.Unspecified),
            (guardada.HoraLocalServicio, guardada.ZonaHoraria, guardada.HoraLocalServicio.Kind));
    }

    [Fact]
    public async Task Registrar_QuedaEnLaColaConSuHistorial_YLasUrgentesVanPrimero()
    {
        var normal = await RegistrarAsync(Peticion(_pasajeroId, Manana));
        var urgente = await RegistrarAsync(Peticion(await PasajeroAsync(), Manana.AddHours(3), urgente: true));

        var cola = await _operador.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>("/api/solicitudes?estado=Pendiente");

        Assert.Equal("Pendiente", normal.Solicitud.Estado);
        Assert.Equal("PanelWeb", normal.Solicitud.Canal);
        Assert.Equal("Minera del Sur", normal.Solicitud.EmpresaClienteNombre);
        Assert.Equal("Pendiente", Assert.Single(normal.Historial).EstadoNuevo);
        Assert.Equal([urgente.Solicitud.Id, normal.Solicitud.Id], cola!.Elementos.Select(s => s.Id));
    }

    [Fact]
    public async Task DatosMinimos_PasadoYSolapamiento_SeRechazan()
    {
        await RegistrarAsync(Peticion(_pasajeroId, Manana));

        var solapada = await _operador.PostAsJsonAsync("/api/solicitudes", Peticion(_pasajeroId, Manana.AddMinutes(30)));
        var pasada = await _operador.PostAsJsonAsync("/api/solicitudes", Peticion(_pasajeroId, DateTimeOffset.UtcNow.AddHours(-1)));
        var incompleta = await _operador.PostAsJsonAsync("/api/solicitudes", Peticion(_pasajeroId, Manana.AddDays(1)) with { DireccionDestino = null });
        var contigua = await _operador.PostAsJsonAsync("/api/solicitudes", Peticion(_pasajeroId, Manana.AddMinutes(45)));

        Assert.Equal(HttpStatusCode.BadRequest, solapada.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pasada.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, incompleta.StatusCode);
        Assert.Equal(HttpStatusCode.Created, contigua.StatusCode);
    }

    [Fact]
    public async Task Itinerarios_Recurrentes_YSolicitudDesdeUnItinerario()
    {
        var lunes = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7 + (((int)DayOfWeek.Monday - (int)DateTime.UtcNow.DayOfWeek + 7) % 7)));
        var peticion = new ItinerariosPeticion
        {
            EmpresaClienteId = _empresaId,
            CodigoServicio = "TURNO-A",
            Origen = "Campamento",
            Destino = "Mina",
            HoraSalida = new TimeOnly(5, 30),
            HoraLlegada = new TimeOnly(6, 15),
            FechaDesde = lunes,
            FechaHasta = lunes.AddDays(13),
            Dias = ["lunes", "martes", "miércoles", "jueves", "viernes"]
        };

        var creados = await (await _operador.PostAsJsonAsync("/api/itinerarios", peticion)).Content.ReadFromJsonAsync<List<ItinerarioRespuesta>>();
        var repetidos = await _operador.PostAsJsonAsync("/api/itinerarios", peticion);
        var listados = await _operador.GetFromJsonAsync<PaginaRespuesta<ItinerarioRespuesta>>(
            $"/api/itinerarios?empresaClienteId={_empresaId}&codigoServicio=TURNO-A&tamanoPagina=50");
        var solicitud = await RegistrarAsync(new SolicitudPeticion { PasajeroId = _pasajeroId, ItinerarioId = creados![0].Id, CantidadPasajeros = 1 });

        Assert.Equal(10, creados.Count);
        Assert.DoesNotContain(creados, i => i.Fecha.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(HttpStatusCode.BadRequest, repetidos.StatusCode);
        Assert.Equal(10, listados!.Total);
        Assert.Equal(("Campamento", "Mina", 45), (solicitud.Solicitud.DireccionOrigen, solicitud.Solicitud.DireccionDestino, solicitud.Solicitud.DuracionMinutos));
        Assert.Equal(new TimeOnly(5, 30), TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(solicitud.Solicitud.FechaHoraServicio, Bogota).DateTime));
        Assert.Equal(creados[0].Id, solicitud.Solicitud.ItinerarioId);
    }

    [Fact]
    public async Task Modificar_SoloSinAsignar_YCancelarConMotivoQuedaEnHistorialYAuditoria()
    {
        var solicitud = await RegistrarAsync(Peticion(_pasajeroId, Manana));

        var modificada = await (await _operador.PutAsJsonAsync($"/api/solicitudes/{solicitud.Solicitud.Id}",
            Peticion(_pasajeroId, Manana.AddHours(1)) with { DireccionDestino = "Terminal", CantidadPasajeros = 2 }))
            .Content.ReadFromJsonAsync<SolicitudDetalleRespuesta>();
        var cancelada = await (await _operador.PostAsJsonAsync($"/api/solicitudes/{solicitud.Solicitud.Id}/cancelar",
            new CancelarSolicitudPeticion { Motivo = "El pasajero ya no viaja" })).Content.ReadFromJsonAsync<SolicitudDetalleRespuesta>();
        var trasCancelar = await _operador.PutAsJsonAsync($"/api/solicitudes/{solicitud.Solicitud.Id}", Peticion(_pasajeroId, Manana));
        var sinMotivo = await _operador.PostAsJsonAsync($"/api/solicitudes/{solicitud.Solicitud.Id}/cancelar", new CancelarSolicitudPeticion());

        Assert.Equal(("Terminal", 2), (modificada!.Solicitud.DireccionDestino, modificada.Solicitud.CantidadPasajeros));
        Assert.Equal("Cancelada", cancelada!.Solicitud.Estado);
        Assert.Equal("El pasajero ya no viaja", cancelada.Historial[^1].Motivo);
        Assert.Equal(HttpStatusCode.BadRequest, trasCancelar.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        await using var silo = await _entorno.AbrirSiloAsync(_tenant);
        var acciones = await silo.Contexto.EventosAuditoria.Where(e => e.RecursoId == solicitud.Solicitud.Id.ToString())
            .OrderBy(e => e.Id).Select(e => e.Accion).ToListAsync();
        Assert.Equal(["solicitud.crear", "solicitud.modificar", "solicitud.cancelar"], acciones);
        Assert.NotNull(await silo.Contexto.Set<CancelacionSolicitud>().SingleOrDefaultAsync(c => c.SolicitudId == solicitud.Solicitud.Id));
    }

    [Fact]
    public async Task VentanaDeCancelacion_SoloElOperadorLogisticoPuedeCancelarFueraDelPlazo()
    {
        var proxima = await RegistrarAsync(Peticion(_pasajeroId, DateTimeOffset.UtcNow.AddMinutes(20)));

        var administrador = await _administrador.PostAsJsonAsync($"/api/solicitudes/{proxima.Solicitud.Id}/cancelar",
            new CancelarSolicitudPeticion { Motivo = "Tarde" });
        var modificar = await _operador.PutAsJsonAsync($"/api/solicitudes/{proxima.Solicitud.Id}",
            Peticion(_pasajeroId, DateTimeOffset.UtcNow.AddMinutes(25)));
        var operador = await _operador.PostAsJsonAsync($"/api/solicitudes/{proxima.Solicitud.Id}/cancelar",
            new CancelarSolicitudPeticion { Motivo = "Contingencia: vía cerrada" });

        Assert.Equal(HttpStatusCode.BadRequest, administrador.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, modificar.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operador.StatusCode);
    }

    [Fact]
    public async Task SolicitudPersonal_SinDireccionesParaQuienNoPuedeVerla()
    {
        var personal = await RegistrarAsync(Peticion(_pasajeroId, Manana, personal: true));
        var ruta = $"/api/solicitudes/{personal.Solicitud.Id}";

        var paraOperador = await _operador.GetFromJsonAsync<SolicitudDetalleRespuesta>(ruta);
        var paraAdministrador = await _administrador.GetFromJsonAsync<SolicitudDetalleRespuesta>(ruta);
        await _administrador.PutAsJsonAsync($"/api/parametros/{CatalogoParametros.VisibilidadSolicitudesPersonales}",
            new CambiarParametroPeticion { Valor = JsonSerializer.SerializeToElement(CatalogoParametros.VisiblesSoloParaPasajero) });
        var paraOperadorTrasCambiar = await _operador.GetFromJsonAsync<SolicitudDetalleRespuesta>(ruta);
        var enListado = await _operador.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>($"/api/solicitudes?pasajeroId={_pasajeroId}");

        Assert.Equal("Hotel Central", paraOperador!.Solicitud.DireccionOrigen);
        Assert.True(paraAdministrador!.Solicitud.DetalleRestringido);
        Assert.Null(paraAdministrador.Solicitud.DireccionOrigen);
        Assert.Null(paraOperadorTrasCambiar!.Solicitud.DireccionDestino);
        Assert.Null(Assert.Single(enListado!.Elementos).DireccionOrigen);
    }

    [Fact]
    public async Task CancelarUnaAsignada_CancelaSuAsignacion_YElHistorialFiltraPorPasajero()
    {
        var solicitud = await RegistrarAsync(Peticion(_pasajeroId, Manana));
        Guid asignacionId;
        await using (var silo = await _entorno.AbrirSiloAsync(_tenant))
        {
            var conductor = new Conductor(Guid.NewGuid(), "Luis", $"+57{Random.Shared.NextInt64(3_000_000_000, 3_999_999_999)}",
                RolesSistema.ConductorId, DateTimeOffset.UtcNow, $"{Random.Shared.Next(10_000_000, 99_999_999)}", "C2", new DateOnly(2030, 1, 1));
            var vehiculo = new Vehiculo(Guid.NewGuid(), $"V{Random.Shared.Next(10_000, 99_999)}", "Toyota", "Hiace", 10, new DateOnly(2030, 1, 1), new DateOnly(2030, 1, 1));
            var entidad = await silo.Contexto.Solicitudes.SingleAsync(s => s.Id == solicitud.Solicitud.Id);
            var asignacion = new Asignacion(Guid.NewGuid(), entidad.Id, conductor.Id, vehiculo.Id, entidad.Franja(), OrigenAsignacion.Manual,
                DateTimeOffset.UtcNow);
            entidad.MarcarAsignada(asignacion, null, DateTimeOffset.UtcNow);
            silo.Contexto.AddRange(conductor, vehiculo, asignacion);
            await silo.Contexto.SaveChangesAsync();
            asignacionId = asignacion.Id;
        }

        var cancelada = await _operador.PostAsJsonAsync($"/api/solicitudes/{solicitud.Solicitud.Id}/cancelar",
            new CancelarSolicitudPeticion { Motivo = "Cambio de turno del pasajero" });
        var historial = await _operador.GetFromJsonAsync<PaginaRespuesta<SolicitudRespuesta>>($"/api/solicitudes?pasajeroId={_pasajeroId}&estado=Cancelada");

        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        Assert.Equal(solicitud.Solicitud.Id, Assert.Single(historial!.Elementos).Id);
        await using var verificacion = await _entorno.AbrirSiloAsync(_tenant);
        Assert.Equal(EstadoAsignacion.Cancelada, (await verificacion.Contexto.Asignaciones.SingleAsync(a => a.Id == asignacionId)).Estado);
    }
}
