using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Notificaciones.Catalogo;
using FlowRide.Application.Notificaciones.Entrada.Dto;
using FlowRide.Application.Notificaciones.Entrada.Puertos;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.GestionNotificaciones;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Notificaciones.Entrada.Adaptadores;

public sealed class NotificacionesAdaptadorEntrada(
    CatalogoMensajes catalogo,
    ICasoUsoConRetorno<ProcesarEventoDominio, bool> procesar,
    ICasoUsoConRetorno<ConsultarCorreosPendientesDominio, IReadOnlyList<Guid>> correosPendientes,
    ICasoUsoConRetorno<EnviarCorreoDominio, bool> enviarCorreo,
    ICasoUsoConRetorno<BuscarAlertasDominio, PaginaResultado<AlertaLectura>> buscarAlertas,
    ICasoUsoConRetorno<AlertasPosterioresDominio, IReadOnlyList<AlertaLectura>> posteriores,
    ICasoUsoConRetorno<MarcarAlertaLeidaDominio, Alerta> marcarLeida,
    ICasoUsoConRetorno<ConsultarPlantillasDominio, IReadOnlyList<PlantillaWhatsApp>> plantillas,
    ICasoUsoConRetorno<ActualizarPlantillaDominio, PlantillaWhatsApp> actualizarPlantilla) : INotificacionesPuertoEntrada
{
    /// <summary>Cuántas alertas manda como máximo cada consulta del envío en vivo.</summary>
    public const int LoteEnVivo = 50;

    public Task<bool> ProcesarEventoAsync(Guid eventId, string tipoEvento, string contenido, DateTimeOffset ocurridoEn, CancellationToken ct) =>
        procesar.EjecutarAsync(new ProcesarEventoDominio(eventId, tipoEvento, contenido, ocurridoEn), ct);

    public Task<IReadOnlyList<Guid>> CorreosPendientesAsync(int maximo, CancellationToken ct) =>
        correosPendientes.EjecutarAsync(new ConsultarCorreosPendientesDominio(maximo), ct);

    public Task<bool> EnviarCorreoAsync(Guid correoId, CancellationToken ct) => enviarCorreo.EjecutarAsync(new EnviarCorreoDominio(correoId), ct);

    public async Task<PaginaRespuesta<AlertaLectura>> BuscarAlertasAsync(BusquedaAlertasPeticion peticion, CancellationToken ct)
    {
        var pagina = await buscarAlertas.EjecutarAsync(new BuscarAlertasDominio(peticion?.SoloNoLeidas ?? false,
            new Paginacion(peticion?.Pagina, peticion?.TamanoPagina)), ct);
        return new PaginaRespuesta<AlertaLectura>(pagina.Elementos, pagina.Total, pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);
    }

    public Task<IReadOnlyList<AlertaLectura>> AlertasPosterioresAsync(long despuesDe, CancellationToken ct) =>
        posteriores.EjecutarAsync(new AlertasPosterioresDominio(despuesDe, LoteEnVivo), ct);

    public async Task<AlertaLeidaRespuesta> MarcarAlertaLeidaAsync(Guid alertaId, Guid? actorId, CancellationToken ct)
    {
        var alerta = await marcarLeida.EjecutarAsync(new MarcarAlertaLeidaDominio(alertaId,
            actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.")), ct);
        return new AlertaLeidaRespuesta(alerta.Id, alerta.LeidaEn);
    }

    public async Task<IReadOnlyList<PlantillaRespuesta>> PlantillasAsync(CancellationToken ct) =>
        (await plantillas.EjecutarAsync(ConsultarPlantillasDominio.Todas, ct)).OrderBy(p => p.Clave, StringComparer.Ordinal).Select(Respuesta).ToList();

    public async Task<PlantillaRespuesta> ActualizarPlantillaAsync(string clave, PlantillaPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion?.Nombre) || string.IsNullOrWhiteSpace(peticion.Idioma) || string.IsNullOrWhiteSpace(peticion.Estado))
            throw new FlowRideExcepcion("nombre, idioma y estado son obligatorios.");
        var estado = Enum.TryParse<EstadoPlantilla>(peticion.Estado.Trim(), ignoreCase: true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw new FlowRideExcepcion($"estado debe ser uno de: {string.Join(", ", Enum.GetNames<EstadoPlantilla>())}.");

        return Respuesta(await actualizarPlantilla.EjecutarAsync(new ActualizarPlantillaDominio(clave, peticion.Nombre.Trim(), peticion.Idioma.Trim(),
            estado, actorId ?? throw new FlowRideExcepcion("La acción requiere un usuario autenticado.")), ct));
    }

    private PlantillaRespuesta Respuesta(PlantillaWhatsApp p) =>
        new(p.Clave, p.Nombre, p.Idioma, p.Estado.ToString(), catalogo.Variables(p.Clave), p.ModificadaEn);
}
