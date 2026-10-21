using System.Net;
using System.Net.Http.Json;
using FlowRide.Application.Asignaciones.Entrada.Dto;
using FlowRide.Application.Asignaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Solicitudes.Entrada.Dto;
using FlowRide.Application.Viajes.Entrada.Dto;
using FlowRide.Application.Viajes.Salida.Puertos;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Infrastructure.Asignaciones;
using FlowRide.IntegrationTests.Asignaciones;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.IntegrationTests.Viajes;

/// <summary>Viajes y calificación (RF-31, RF-34, RF-37, RF-40, RF-54) con la API real y PostgreSQL real.</summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class ViajesApiTests(PostgresFixture postgres) : PruebasAsignacionBase(postgres)
{
    private async Task<ViajeRespuesta> AceptarAsync(Guid asignacionId)
    {
        var respuesta = await Operador.PostAsync($"/api/asignaciones/{asignacionId}/aceptar", null);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ViajeRespuesta>())!;
    }

    private Task<long> OutboxAsync(string tipo) =>
        EjecutarEnSiloAsync<long>($"SELECT count(*) FROM notif.outbox_mensaje WHERE tipo_evento = '{tipo}'");

    [Fact]
    public async Task CicloCompleto_AceptarIniciarParadaIncidenteFinalizarYCalificar()
    {
        await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();
        var asignacion = await AsignarAsync(solicitudId);

        var porAdministrador = await Administrador.PostAsync($"/api/asignaciones/{asignacion.AsignacionId}/aceptar", null);
        var viaje = await AceptarAsync(asignacion.AsignacionId!.Value);
        var parada = await Operador.PostAsJsonAsync($"/api/viajes/{viaje.Id}/paradas",
            new ParadaPeticion { Direccion = "Portería norte", Orden = 1, HoraEstimada = Servicio.AddMinutes(20).HoraLocal() });
        var retraso = await Operador.PostAsJsonAsync($"/api/viajes/{viaje.Id}/incidentes",
            new IncidentePeticion { Tipo = "Retraso", Descripcion = "Vía cerrada por derrumbe" });
        var iniciado = await Operador.PostAsync($"/api/viajes/{viaje.Id}/iniciar", null);
        var paradaId = (await parada.Content.ReadFromJsonAsync<ParadaRespuesta>())!.Id;
        var llegada = await Operador.PostAsync($"/api/viajes/{viaje.Id}/paradas/{paradaId}/llegada", null);
        var finalizado = await Operador.PostAsync($"/api/viajes/{viaje.Id}/finalizar", null);
        var calificacion = await Operador.PostAsJsonAsync($"/api/viajes/{viaje.Id}/calificacion",
            new CalificacionPeticion { Puntaje = 5, Comentario = "Muy amable" });
        var repetida = await Operador.PostAsJsonAsync($"/api/viajes/{viaje.Id}/calificacion", new CalificacionPeticion { Puntaje = 3 });

        Assert.Equal(HttpStatusCode.Forbidden, porAdministrador.StatusCode);
        Assert.Equal("Programado", viaje.Estado);
        Assert.Equal(HttpStatusCode.Created, parada.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retraso.StatusCode);
        Assert.Equal("EnCurso", (await iniciado.Content.ReadFromJsonAsync<ViajeRespuesta>())!.Estado);
        Assert.Equal(HttpStatusCode.OK, llegada.StatusCode);
        Assert.Equal("Finalizado", (await finalizado.Content.ReadFromJsonAsync<ViajeRespuesta>())!.Estado);
        Assert.Equal(HttpStatusCode.Created, calificacion.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, repetida.StatusCode);

        var detalle = (await Administrador.GetFromJsonAsync<ViajeDetalleLectura>($"/api/viajes/{viaje.Id}"))!;
        Assert.Equal((EstadoViaje.Finalizado, solicitudId, 1, (int?)5), (detalle.Viaje.Estado, detalle.Viaje.SolicitudId, detalle.Viaje.Incidentes,
            detalle.Viaje.Puntaje));
        Assert.NotNull(Assert.Single(detalle.Paradas).HoraReal);
        Assert.Equal(TipoIncidente.Retraso, Assert.Single(detalle.Incidentes).Tipo);
        Assert.Equal("Muy amable", detalle.Calificacion!.Comentario);
        Assert.Equal("Completada", (await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{solicitudId}"))!.Solicitud.Estado);

        var historial = (await Operador.GetFromJsonAsync<PaginaRespuesta<ViajeLectura>>("/api/viajes?estado=Finalizado"))!;
        Assert.Equal(viaje.Id, Assert.Single(historial.Elementos).Id);

        var traza = (await Operador.GetFromJsonAsync<TrazabilidadLectura>($"/api/solicitudes/{solicitudId}/trazabilidad"))!;
        var tipos = traza.Eventos.Select(e => e.Tipo).ToList();
        foreach (var esperado in new[] { "solicitud", "decision", "asignacion", "asignacion.aceptar", "viaje.registrar_parada", "incidente",
                     "viaje.iniciar", "parada", "viaje.finalizar", "calificacion" })
            Assert.Contains(esperado, tipos);
        Assert.Equal(traza.Eventos.OrderBy(e => e.FechaHora).Select(e => e.FechaHora), traza.Eventos.Select(e => e.FechaHora));
        Assert.True(tipos.IndexOf("asignacion.aceptar") < tipos.IndexOf("viaje.iniciar"));
        Assert.True(tipos.IndexOf("viaje.iniciar") < tipos.IndexOf("viaje.finalizar"));

        Assert.Equal(1L, await OutboxAsync("asignacion.aceptada"));
        Assert.Equal(1L, await OutboxAsync("viaje.iniciado"));
        Assert.Equal(1L, await OutboxAsync("viaje.finalizado"));
        Assert.Equal(1L, await OutboxAsync("incidente.reportado"));
    }

    [Fact]
    public async Task Rechazar_DevuelveALaCola_YElWorkerLaAsignaAOtroConductor()
    {
        var primero = await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();
        var asignacion = await AsignarAsync(solicitudId);
        var otro = await ConductorListoAsync();
        Assert.Equal(primero.ConductorId, asignacion.ConductorId);

        var sinMotivo = await Operador.PostAsJsonAsync($"/api/asignaciones/{asignacion.AsignacionId}/rechazar", new MotivoPeticion());
        var rechazo = await Operador.PostAsJsonAsync($"/api/asignaciones/{asignacion.AsignacionId}/rechazar",
            new MotivoPeticion { Motivo = "Vehículo en taller" });
        var enCola = await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{solicitudId}");
        await Api.Services.GetRequiredService<WorkerColaAsignacion>().ProcesarAsync(CancellationToken.None);
        var asignaciones = (await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?solicitudId={solicitudId}"))!;
        var ultima = (await Operador.GetFromJsonAsync<List<DecisionLectura>>($"/api/solicitudes/{solicitudId}/decisiones"))![0];

        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rechazo.StatusCode);
        Assert.Equal("Pendiente", enCola!.Solicitud.Estado);
        var vigente = Assert.Single(asignaciones.Elementos, a => a.Estado == EstadoAsignacion.Asignada);
        Assert.Equal(otro.ConductorId, vigente.ConductorId);
        Assert.Equal("Vehículo en taller", Assert.Single(asignaciones.Elementos, a => a.Estado == EstadoAsignacion.Rechazada).MotivoCierre);
        Assert.Contains("rechazó", ultima.Evaluaciones.Single(e => e.ConductorId == primero.ConductorId).MotivoDescarte);
        Assert.Equal(1L, await OutboxAsync("asignacion.rechazada"));
    }

    [Fact]
    public async Task ElWorker_VenceLasAsignacionesSinRespuesta()
    {
        await ConductorListoAsync();
        var solicitudId = await SolicitudAsync();
        var asignacion = await AsignarAsync(solicitudId);
        await EjecutarEnSiloAsync<int>(
            $"UPDATE ops.asignacion SET asignada_en = asignada_en - interval '1 hour' WHERE id = '{asignacion.AsignacionId}' RETURNING 1");

        await Api.Services.GetRequiredService<WorkerColaAsignacion>().ProcesarAsync(CancellationToken.None);

        var asignaciones = (await Operador.GetFromJsonAsync<PaginaRespuesta<AsignacionLectura>>($"/api/asignaciones?solicitudId={solicitudId}"))!;
        var vencida = Assert.Single(asignaciones.Elementos);
        Assert.Equal((EstadoAsignacion.Rechazada, "El conductor no respondió en 15 minutos."), (vencida.Estado, vencida.MotivoCierre));
        // Era el único conductor y ya no se le vuelve a proponer: el operador recibe la alerta de sin candidatos.
        Assert.Equal("SinCandidatos", (await Operador.GetFromJsonAsync<SolicitudDetalleRespuesta>($"/api/solicitudes/{solicitudId}"))!.Solicitud.Estado);
        var traza = (await Operador.GetFromJsonAsync<TrazabilidadLectura>($"/api/solicitudes/{solicitudId}/trazabilidad"))!;
        var vencimiento = Assert.Single(traza.Eventos, e => e.Tipo == "asignacion.vencer");
        Assert.Null(vencimiento.ActorId);
    }

    [Fact]
    public async Task Monitoreo_MuestraLosViajesEnCurso_YCancelarLaSolicitudCancelaElViajeProgramado()
    {
        await ConductorListoAsync();
        await ConductorListoAsync();
        var enCursoId = await SolicitudAsync();
        var programadoId = await SolicitudAsync(Servicio.AddMinutes(5));
        var enCurso = await AceptarAsync((await AsignarAsync(enCursoId)).AsignacionId!.Value);
        var programado = await AceptarAsync((await AsignarAsync(programadoId)).AsignacionId!.Value);
        await Operador.PostAsync($"/api/viajes/{enCurso.Id}/iniciar", null);

        var monitoreo = (await Administrador.GetFromJsonAsync<List<MonitoreoViajeLectura>>("/api/viajes/monitoreo"))!;
        var cancelar = await Operador.PostAsJsonAsync($"/api/solicitudes/{programadoId}/cancelar", new CancelarSolicitudPeticion { Motivo = "Turno suspendido" });
        var cancelado = (await Operador.GetFromJsonAsync<ViajeDetalleLectura>($"/api/viajes/{programado.Id}"))!;
        var iniciarCancelado = await Operador.PostAsync($"/api/viajes/{programado.Id}/iniciar", null);

        // El programado empieza en tres días: fuera del horizonte de monitoreo.
        Assert.Equal(enCurso.Id, Assert.Single(monitoreo).Viaje.Id);
        Assert.Equal(HttpStatusCode.OK, cancelar.StatusCode);
        Assert.Equal(EstadoViaje.Cancelado, cancelado.Viaje.Estado);
        Assert.Equal(HttpStatusCode.BadRequest, iniciarCancelado.StatusCode);
    }
}
