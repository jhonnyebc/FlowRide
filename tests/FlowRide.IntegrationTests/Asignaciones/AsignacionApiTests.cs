using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Infrastructure.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FlowRide.IntegrationTests.Asignaciones;

/// <summary>Base de las pruebas de asignación: un Silo propio con conductores, vehículos y solicitudes creados por la API.</summary>
public abstract class PruebasAsignacionBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected static readonly DateTimeOffset Servicio = DateTimeOffset.UtcNow.Date.AddDays(3).ToUniversalTime().AddHours(14);
    protected static readonly DateOnly Vigente = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3));

    protected string Tenant { get; } = $"asg-{Guid.NewGuid():N}"[..14];
    protected EntornoAprovisionamiento Entorno { get; private set; } = null!;
    protected FlowRideApiFactory Api { get; private set; } = null!;
    protected HttpClient Administrador { get; private set; } = null!;
    protected HttpClient Operador { get; private set; } = null!;
    private Guid _empresaId;

    public async Task InitializeAsync()
    {
        Entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await Entorno.AprovisionarAsync(Tenant);
        Api = await ApiAsync();
        Administrador = await ClienteAsync(Api, RolesSistema.AdministradorId);
        Operador = await ClienteAsync(Api, RolesSistema.OperadorLogisticoId);
        _empresaId = (await (await Administrador.PostAsJsonAsync("/api/empresas-cliente",
                new EmpresaClientePeticion { Nombre = "Minera", Nit = $"nit-{Guid.NewGuid():N}"[..14], CorreoContacto = "ops@minera.co" }))
            .Content.ReadFromJsonAsync<EmpresaClienteRespuesta>())!.Id;
    }

    public async Task DisposeAsync()
    {
        await Api.DisposeAsync();
        await Entorno.DisposeAsync();
    }

    protected async Task<FlowRideApiFactory> ApiAsync() =>
        new(await Entorno.CadenaDelSiloAsync(Tenant), Tenant)
        {
            ConTokensDePrueba = true,
            Proveedor = new ProveedorIdentidadDePrueba(),
            Ajustes = await Entorno.AjustesDelStackAsync(Tenant)
        };

    protected async Task<HttpClient> ClienteAsync(FlowRideApiFactory api, Guid rolId)
    {
        var sub = $"sub-{Guid.NewGuid():N}";
        await using (var silo = await Entorno.AbrirSiloAsync(Tenant))
        {
            silo.Contexto.Usuarios.Add(new Usuario(Guid.NewGuid(), "Usuario del panel", $"+1{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
                rolId, DateTimeOffset.UtcNow, null, sub));
            await silo.Contexto.SaveChangesAsync();
        }
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokensDePrueba.Crear(sub, Tenant));
        return cliente;
    }

    private static string Telefono() => $"+57{Random.Shared.NextInt64(3_000_000_000, 3_999_999_999)}";

    /// <summary>Conductor con un turno que cubre el servicio y su vehículo habitual.</summary>
    protected async Task<(Guid ConductorId, Guid VehiculoId, Guid TurnoId)> ConductorListoAsync(int capacidad = 10, bool aptoCritico = false)
    {
        var conductor = (await (await Administrador.PostAsJsonAsync("/api/conductores", new ConductorPeticion
        {
            Nombre = $"Conductor {Guid.NewGuid():N}"[..18],
            TelefonoWhatsApp = Telefono(),
            NumeroLicencia = Random.Shared.NextInt64(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
            CategoriaLicencia = "C2",
            VencimientoLicencia = Vigente,
            AptoServicioCritico = aptoCritico
        })).Content.ReadFromJsonAsync<ConductorRespuesta>())!;
        var turno = (await (await Administrador.PostAsJsonAsync($"/api/conductores/{conductor.Id}/turnos",
            new TurnoPeticion { Inicio = Servicio.AddHours(-4), Fin = Servicio.AddHours(8) })).Content.ReadFromJsonAsync<TurnoRespuesta>())!;
        var vehiculo = (await (await Administrador.PostAsJsonAsync("/api/vehiculos", new VehiculoPeticion
        {
            Placa = $"T{Random.Shared.Next(10_000, 99_999)}",
            Marca = "Toyota",
            Modelo = "Hiace",
            Capacidad = capacidad,
            VencimientoRtm = Vigente,
            VencimientoSoat = Vigente
        })).Content.ReadFromJsonAsync<VehiculoRespuesta>())!;
        await Administrador.PutAsJsonAsync($"/api/vehiculos/{vehiculo.Id}/conductor-habitual", new ConductorHabitualPeticion { ConductorId = conductor.Id });
        return (conductor.Id, vehiculo.Id, turno.Id);
    }

    protected async Task<Guid> SolicitudAsync(DateTimeOffset? servicio = null, bool urgente = false)
    {
        var pasajero = (await (await Administrador.PostAsJsonAsync("/api/pasajeros", new PasajeroPeticion
        {
            Nombre = "Pasajero",
            TelefonoWhatsApp = Telefono(),
            EmpresaClienteId = _empresaId,
            CodigoInterno = $"E-{Guid.NewGuid():N}"[..12]
        })).Content.ReadFromJsonAsync<PasajeroRespuesta>())!;
        var respuesta = await Operador.PostAsJsonAsync("/api/solicitudes", new SolicitudPeticion
        {
            PasajeroId = pasajero.Id,
            DireccionOrigen = "Campamento",
            DireccionDestino = "Mina",
            FechaHoraServicio = servicio ?? Servicio,
            DuracionMinutos = 60,
            CantidadPasajeros = 2,
            Urgente = urgente
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<SolicitudDetalleRespuesta>())!.Solicitud.Id;
    }

    protected async Task<ResultadoAsignacionRespuesta> AsignarAsync(Guid solicitudId) =>
        (await (await Operador.PostAsync($"/api/solicitudes/{solicitudId}/asignar", null)).Content.ReadFromJsonAsync<ResultadoAsignacionRespuesta>())!;

    protected async Task<T> EjecutarEnSiloAsync<T>(string sql)
    {
        var baseDelSilo = new NpgsqlConnectionStringBuilder(Entorno.CadenaAdministracion) { Database = $"flowride_{Tenant.Replace('-', '_')}" };
        await using var fuente = NpgsqlDataSource.Create(baseDelSilo.ConnectionString);
        return (T)(await fuente.CreateCommand(sql).ExecuteScalarAsync())!;
    }
}

/// <summary>Asignación de punta a punta (RF-10 a RF-13, RF-21, RF-38; ADR-0007, ADR-0009) con la API real y PostgreSQL real.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class AsignacionApiTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    [Fact]
    public async Task AsignarYa_UsaElMotor_GuardaElDecisionLog_YDejaElEventoEnLaOutbox()
    {
        var primero = await ConductorListoAsync();
        var segundo = await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();

        var resultado = await AsignarAsync(solicitudId);
        var decisiones = await Operador.GetFromJsonAsync<List<DecisionLectura>>($"/api/solicitudes/{solicitudId}/decisiones");
        var asignaciones = await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?solicitudId={solicitudId}");
        var detalle = await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{solicitudId}");

        Assert.Equal("Asignada", resultado.Resultado);
        Assert.Contains(resultado.ConductorId, new Guid?[] { primero.ConductorId, segundo.ConductorId });
        var decision = Assert.Single(decisiones!);
        Assert.Equal((1, "Assigned", 2), (decision.VersionConfiguracion, decision.Resultado, decision.Evaluaciones.Count));
        Assert.All(decision.Evaluaciones, e => Assert.Contains(e.Resultados, r => r.CodigoRegla == "AvailabilityRule" && r.VersionRegla == 2));
        Assert.Equal(resultado.AsignacionId, Assert.Single(asignaciones!.Elementos).Id);
        Assert.Equal("Asignada", detalle!.Solicitud.Estado);
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM notif.outbox_mensaje WHERE tipo_evento = 'asignacion.creada'"));
    }

    [Fact]
    public async Task SinCandidatos_AvisaYSeDevuelveALaColaCuandoHayConductor()
    {
        var solicitudId = await SolicitudAsync();

        var sinCandidatos = await AsignarAsync(solicitudId);
        await ConductorListoAsync();
        var devolver = await Operador.PostAsJsonAsync($"/api/solicitudes/{solicitudId}/devolver-a-cola", new MotivoPeticion { Motivo = "Llegó un conductor" });
        var asignada = await AsignarAsync(solicitudId);

        Assert.Equal("SinCandidatos", sinCandidatos.Resultado);
        Assert.Equal(1L, await EjecutarEnSiloAsync<long>("SELECT count(*) FROM notif.outbox_mensaje WHERE tipo_evento = 'solicitud.sin_candidatos'"));
        Assert.Equal(HttpStatusCode.NoContent, devolver.StatusCode);
        Assert.Equal("Asignada", asignada.Resultado);
    }

    [Fact]
    public async Task SoloElOperadorLogisticoReasigna_YElCandidatoDebeCumplirLasReglas()
    {
        await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();
        var inicial = await AsignarAsync(solicitudId);
        var otro = await ConductorListoAsync();
        var pequeno = await ConductorListoAsync(capacidad: 1);
        var peticion = new CandidatoManualPeticion { ConductorId = otro.ConductorId, VehiculoId = otro.VehiculoId, Motivo = "Conductor enfermo" };

        var porAdministrador = await Administrador.PostAsJsonAsync($"/api/asignaciones/{inicial.AsignacionId}/reasignar", peticion);
        var invalida = await Operador.PostAsJsonAsync($"/api/asignaciones/{inicial.AsignacionId}/reasignar",
            peticion with { ConductorId = pequeno.ConductorId, VehiculoId = pequeno.VehiculoId });
        var reasignada = await Operador.PostAsJsonAsync($"/api/asignaciones/{inicial.AsignacionId}/reasignar", peticion);
        var historial = await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?solicitudId={solicitudId}");

        Assert.Equal(HttpStatusCode.Forbidden, porAdministrador.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reasignada.StatusCode);
        Assert.Equal(otro.ConductorId, (await reasignada.Content.ReadFromJsonAsync<AsignacionRespuesta>())!.ConductorId);
        Assert.Equal(["Asignada", "Reemplazada"], historial!.Elementos.Select(a => a.Estado.ToString()).Order());
        Assert.Equal(2, (await Operador.GetFromJsonAsync<List<DecisionLectura>>($"/api/solicitudes/{solicitudId}/decisiones"))!.Count);
    }

    [Fact]
    public async Task AbandonarElTurno_DevuelveSusSolicitudesALaCola()
    {
        var conductor = await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();
        await AsignarAsync(solicitudId);

        var respuesta = await Operador.PostAsJsonAsync($"/api/conductores/{conductor.ConductorId}/turnos/{conductor.TurnoId}/abandonar",
            new MotivoPeticion { Motivo = "Emergencia familiar" });
        var detalle = await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{solicitudId}");
        var asignaciones = await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?conductorId={conductor.ConductorId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Pendiente", detalle!.Solicitud.Estado);
        Assert.Equal(EstadoAsignacion.Cancelada, Assert.Single(asignaciones!.Elementos).Estado);
    }

    [Fact]
    public async Task ConfiguracionDelMotor_VersionadaConActivacionYRollback()
    {
        var iniciales = (await Operador.GetFromJsonAsync<List<ConfiguracionMotorRespuesta>>("/api/motor/configuraciones"))!;
        var reglas = iniciales.Single().Reglas
            .Select(r => new ReglaPeticion { Codigo = r.Codigo, Habilitada = r.Habilitada, Orden = r.Orden, Peso = r.Codigo == "WorkloadBalanceRule" ? 0.5m : r.Peso, Parametros = r.Parametros })
            .ToList();

        var invalida = await Administrador.PostAsJsonAsync("/api/motor/configuraciones", new ConfiguracionMotorPeticion
        {
            Reglas = reglas.Select(r => r.Codigo == "AvailabilityRule" ? r with { Habilitada = false } : r).ToList()
        });
        var porOperador = await Operador.PostAsJsonAsync("/api/motor/configuraciones", new ConfiguracionMotorPeticion { Reglas = reglas });
        var creada = await Administrador.PostAsJsonAsync("/api/motor/configuraciones", new ConfiguracionMotorPeticion { Reglas = reglas });
        var activada = await Administrador.PostAsync("/api/motor/configuraciones/2/activar", null);
        await ConductorListoAsync();
        var conV2 = await AsignarAsync(await SolicitudAsync());
        var rollback = await Administrador.PostAsync("/api/motor/configuraciones/1/activar", null);
        var finales = await Operador.GetFromJsonAsync<List<ConfiguracionMotorRespuesta>>("/api/motor/configuraciones");

        Assert.Equal(("Activa", 7), (iniciales.Single().Estado, iniciales.Single().Reglas.Count));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, porOperador.StatusCode);
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, activada.StatusCode);
        Assert.Equal(2, (await Operador.GetFromJsonAsync<List<DecisionLectura>>($"/api/solicitudes/{conV2.SolicitudId}/decisiones"))!.Single().VersionConfiguracion);
        Assert.Equal(HttpStatusCode.OK, rollback.StatusCode);
        Assert.Equal([("Archivada", 2), ("Activa", 1)], finales!.Select(c => (c.Estado, c.Version)));
    }

    [Fact]
    public async Task ElDecisionLog_SoloAdmiteInserciones()
    {
        await AsignarAsync(await SolicitudAsync());

        var error = await Assert.ThrowsAsync<PostgresException>(() => EjecutarEnSiloAsync<int>("UPDATE motor.decision_asignacion SET motivo = 'otro'"));

        Assert.Contains("solo admite inserciones", error.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ElWorker_AsignaLaColaConLasUrgentesPrimero()
    {
        var conductor = await ConductorListoAsync(aptoCritico: true);
        var normal = await SolicitudAsync();
        var urgente = await SolicitudAsync(urgente: true);

        var procesadas = await Api.Services.GetRequiredService<WorkerColaAsignacion>().ProcesarAsync(CancellationToken.None);

        Assert.Equal(2, procesadas);
        var cola = await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?conductorId={conductor.ConductorId}");
        Assert.Equal(urgente, Assert.Single(cola!.Elementos).SolicitudId);
        Assert.Equal("SinCandidatos", (await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{normal}"))!.Solicitud.Estado);
    }
}

/// <summary>
/// Spike 2 (ADR-0007): con más solicitudes que conductores en la misma franja y dos réplicas del worker compitiendo,
/// nunca hay dos asignaciones vigentes solapadas para el mismo conductor o vehículo.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class Spike2DoblesAsignacionesTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    [Fact]
    public async Task DosReplicasCompitiendo_CeroDoblesAsignaciones()
    {
        const int conductores = 3;
        const int solicitudes = 12;
        for (var i = 0; i < conductores; i++) await ConductorListoAsync();
        for (var i = 0; i < solicitudes; i++) await SolicitudAsync(Servicio.AddMinutes(i % 3 * 10));

        await using var otraReplica = await ApiAsync();
        var workers = new[] { Api, otraReplica }.Select(api => api.Services.GetRequiredService<WorkerColaAsignacion>()).ToArray();
        for (var vuelta = 0; vuelta < 10; vuelta++)
        {
            await Task.WhenAll(workers.SelectMany(worker => Enumerable.Range(0, 2).Select(_ => worker.ProcesarAsync(CancellationToken.None))));
            if (await EjecutarEnSiloAsync<long>("SELECT count(*) FROM ops.solicitud_viaje WHERE estado = 'Pendiente'") == 0)
                break;
        }

        var asignadas = await EjecutarEnSiloAsync<long>("SELECT count(*) FROM ops.asignacion WHERE estado IN ('Asignada', 'Aceptada', 'EnCurso')");
        var sinCandidatos = await EjecutarEnSiloAsync<long>("SELECT count(*) FROM ops.solicitud_viaje WHERE estado = 'SinCandidatos'");
        var solapadas = await EjecutarEnSiloAsync<long>("""
            SELECT count(*) FROM ops.asignacion a JOIN ops.asignacion b ON a.id < b.id
             AND (a.conductor_id = b.conductor_id OR a.vehiculo_id = b.vehiculo_id)
             AND tstzrange(a.franja_inicio, a.franja_fin) && tstzrange(b.franja_inicio, b.franja_fin)
            WHERE a.estado IN ('Asignada', 'Aceptada', 'EnCurso') AND b.estado IN ('Asignada', 'Aceptada', 'EnCurso')
            """);

        Assert.Equal(0L, solapadas);
        Assert.Equal(conductores, asignadas);
        Assert.Equal(solicitudes - conductores, sinCandidatos);
        await using var silo = await Entorno.AbrirSiloAsync(Tenant);
        Assert.Equal(solicitudes, await silo.Contexto.Solicitudes.CountAsync(s => s.Estado != EstadoSolicitud.Pendiente));
    }
}
