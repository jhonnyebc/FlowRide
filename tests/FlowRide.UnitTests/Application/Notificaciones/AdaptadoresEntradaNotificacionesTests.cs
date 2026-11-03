using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Notificaciones.Catalogo;
using FlowRide.Application.Notificaciones.Entrada.Adaptadores;
using FlowRide.Application.Notificaciones.Entrada.Dto;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.GestionNotificaciones;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Notificaciones;

public class AdaptadoresEntradaNotificacionesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class Grabador<TEntrada, TSalida>(Func<TEntrada, TSalida> responder) : ICasoUsoConRetorno<TEntrada, TSalida>
    {
        public List<TEntrada> Recibidos { get; } = [];

        public Task<TSalida> EjecutarAsync(TEntrada datos, CancellationToken ct)
        {
            Recibidos.Add(datos);
            return Task.FromResult(responder(datos));
        }
    }

    private readonly Grabador<ProcesarEventoDominio, bool> _procesar = new(_ => true);
    private readonly Grabador<ConsultarCorreosPendientesDominio, IReadOnlyList<Guid>> _correos = new(_ => [Guid.Empty]);
    private readonly Grabador<EnviarCorreoDominio, bool> _enviar = new(_ => true);
    private readonly Grabador<BuscarAlertasDominio, PaginaResultado<AlertaLectura>> _buscar = new(d => new PaginaResultado<AlertaLectura>([], 0, d.Paginacion));
    private readonly Grabador<AlertasPosterioresDominio, IReadOnlyList<AlertaLectura>> _posteriores = new(_ => []);
    private readonly Grabador<MarcarAlertaLeidaDominio, Alerta> _leida = new(d =>
    {
        var alerta = new Alerta(d.AlertaId, "alerta.x", SeveridadAlerta.Info, "t", "d", Ahora);
        alerta.MarcarLeida(d.ActorId, Ahora);
        return alerta;
    });
    private readonly Grabador<ConsultarPlantillasDominio, IReadOnlyList<PlantillaWhatsApp>> _plantillas =
        new(_ => [new PlantillaWhatsApp("viaje.pasajero.iniciado", "viaje_pasajero_iniciado", "es_CO")]);
    private readonly Grabador<ActualizarPlantillaDominio, PlantillaWhatsApp> _actualizar =
        new(d => new PlantillaWhatsApp(d.Clave, d.Nombre, d.Idioma, d.Estado));

    private NotificacionesAdaptadorEntrada Adaptador() => new(CatalogoMensajes.Cargar(), _procesar, _correos, _enviar, _buscar, _posteriores,
        _leida, _plantillas, _actualizar);

    [Fact]
    public async Task TraduceLasPeticiones()
    {
        var adaptador = Adaptador();
        var eventId = Guid.NewGuid();

        Assert.True(await adaptador.ProcesarEventoAsync(eventId, "viaje.iniciado", "{}", Ahora, default));
        Assert.Equal([Guid.Empty], await adaptador.CorreosPendientesAsync(5, default));
        Assert.True(await adaptador.EnviarCorreoAsync(Guid.Empty, default));
        await adaptador.BuscarAlertasAsync(new BusquedaAlertasPeticion { SoloNoLeidas = true, Pagina = 2 }, default);
        await adaptador.AlertasPosterioresAsync(7, default);
        var leida = await adaptador.MarcarAlertaLeidaAsync(Guid.NewGuid(), Actor, default);
        var plantillas = await adaptador.PlantillasAsync(default);

        Assert.Equal(eventId, Assert.Single(_procesar.Recibidos).EventId);
        Assert.Equal((true, 2), (_buscar.Recibidos[0].SoloNoLeidas, _buscar.Recibidos[0].Paginacion.Pagina));
        Assert.Equal((7L, NotificacionesAdaptadorEntrada.LoteEnVivo), (_posteriores.Recibidos[0].DespuesDe, _posteriores.Recibidos[0].Maximo));
        Assert.Equal((DateTimeOffset?)Ahora, leida.LeidaEn);
        Assert.Equal(["conductor"], Assert.Single(plantillas).Variables);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.MarcarAlertaLeidaAsync(Guid.NewGuid(), null, default));
    }

    [Fact]
    public async Task ActualizarPlantilla_ValidaEstadoYCampos()
    {
        var adaptador = Adaptador();

        var plantilla = await adaptador.ActualizarPlantillaAsync("viaje.pasajero.iniciado",
            new PlantillaPeticion { Nombre = " viaje_iniciado ", Idioma = "es_CO", Estado = "aprobada" }, Actor, default);

        Assert.Equal(("viaje_iniciado", "Aprobada"), (plantilla.Nombre, plantilla.Estado));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ActualizarPlantillaAsync("viaje.pasajero.iniciado",
            new PlantillaPeticion { Nombre = "x", Idioma = "es", Estado = "Publicada" }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ActualizarPlantillaAsync("viaje.pasajero.iniciado",
            new PlantillaPeticion { Nombre = "x" }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ActualizarPlantillaAsync("viaje.pasajero.iniciado",
            new PlantillaPeticion { Nombre = "x", Idioma = "es", Estado = "Aprobada" }, null, default));
    }
}
