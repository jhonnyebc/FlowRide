using System.Diagnostics.CodeAnalysis;
using FlowRide.Application.Comun;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Operacion.CasosUso;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Operacion.Entrada.Puertos;
using FlowRide.Domain.Comun;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Operacion.Entrada.Adaptadores;

public sealed class EmpresasClienteAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarEmpresaClienteDominio, EmpresaCliente> registrar,
    ICasoUsoConRetorno<ActualizarEmpresaClienteDominio, EmpresaCliente> actualizar,
    ICasoUsoConRetorno<CambiarEstadoEmpresaClienteDominio, EmpresaCliente> cambiarEstado,
    ICasoUsoConRetorno<BuscarEmpresasClienteDominio, PaginaResultado<EmpresaCliente>> buscar) : IEmpresasClientePuertoEntrada
{
    public async Task<EmpresaClienteRespuesta> RegistrarAsync(EmpresaClientePeticion peticion, Guid? actorId, CancellationToken ct)
    {
        Requeridos.Validar(peticion, "nombre, nit y correoContacto son obligatorios.", peticion?.Nombre, peticion?.Nit, peticion?.CorreoContacto);
        return Mapeo.Empresa(await registrar.EjecutarAsync(
            new RegistrarEmpresaClienteDominio(peticion.Nombre!.Trim(), peticion.Nit!.Trim(), peticion.CorreoContacto!.Trim(), actorId), ct));
    }

    public async Task<EmpresaClienteRespuesta> ActualizarAsync(Guid empresaId, EmpresaClientePeticion peticion, Guid? actorId, CancellationToken ct)
    {
        Requeridos.Validar(peticion, "nombre y correoContacto son obligatorios.", peticion?.Nombre, peticion?.CorreoContacto);
        return Mapeo.Empresa(await actualizar.EjecutarAsync(
            new ActualizarEmpresaClienteDominio(empresaId, peticion.Nombre!.Trim(), peticion.CorreoContacto!.Trim(), actorId), ct));
    }

    public async Task<EmpresaClienteRespuesta> CambiarEstadoAsync(Guid empresaId, bool activa, Guid? actorId, CancellationToken ct) =>
        Mapeo.Empresa(await cambiarEstado.EjecutarAsync(new CambiarEstadoEmpresaClienteDominio(empresaId, activa, actorId), ct));

    public async Task<PaginaRespuesta<EmpresaClienteRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct)
    {
        var resultado = await buscar.EjecutarAsync(new BuscarEmpresasClienteDominio(
            new FiltroRecursos(Requeridos.Texto(peticion?.Texto), peticion?.Activo), Mapeo.Paginacion(peticion)), ct);
        return Mapeo.Pagina(resultado, Mapeo.Empresa);
    }
}

public sealed class PasajerosAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarPasajeroDominio, Pasajero> registrar,
    ICasoUsoConRetorno<ActualizarPasajeroDominio, Pasajero> actualizar,
    ICasoUsoConRetorno<BuscarPasajerosDominio, PaginaResultado<Pasajero>> buscar) : IPasajerosPuertoEntrada
{
    public async Task<PasajeroRespuesta> RegistrarAsync(PasajeroPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        Requeridos.Validar(peticion, "nombre, telefonoWhatsApp, empresaClienteId y codigoInterno son obligatorios.",
            peticion?.Nombre, peticion?.TelefonoWhatsApp, peticion?.EmpresaClienteId?.ToString(), peticion?.CodigoInterno);
        return Mapeo.Pasajero(await registrar.EjecutarAsync(new RegistrarPasajeroDominio(
            peticion.Nombre!.Trim(), TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp), Requeridos.Texto(peticion.Correo),
            peticion.EmpresaClienteId!.Value, peticion.CodigoInterno!.Trim(), actorId), ct));
    }

    public async Task<PasajeroRespuesta> ActualizarAsync(Guid pasajeroId, PasajeroPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        Requeridos.Validar(peticion, "nombre y telefonoWhatsApp son obligatorios.", peticion?.Nombre, peticion?.TelefonoWhatsApp);
        return Mapeo.Pasajero(await actualizar.EjecutarAsync(new ActualizarPasajeroDominio(
            pasajeroId, peticion.Nombre!.Trim(), TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp), Requeridos.Texto(peticion.Correo), actorId), ct));
    }

    public async Task<PaginaRespuesta<PasajeroRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct)
    {
        var resultado = await buscar.EjecutarAsync(new BuscarPasajerosDominio(
            new FiltroPasajeros(Requeridos.Texto(peticion?.Texto), peticion?.EmpresaClienteId, peticion?.Activo),
            Mapeo.Paginacion(peticion)), ct);
        return Mapeo.Pagina(resultado, Mapeo.Pasajero);
    }
}

public sealed class ConductoresAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarConductorDominio, Conductor> registrar,
    ICasoUsoConRetorno<ActualizarConductorDominio, Conductor> actualizar,
    ICasoUsoConRetorno<CambiarEstadoConductorDominio, Conductor> cambiarEstado,
    ICasoUsoConRetorno<Guid, Conductor> consultar,
    ICasoUsoConRetorno<BuscarConductoresDominio, PaginaResultado<Conductor>> buscar,
    ICasoUsoConRetorno<ProgramarTurnoDominio, Turno> programarTurno,
    ICasoUsoConRetorno<ConsultarDisponibilidadDominio, IReadOnlyList<Conductor>> disponibles,
    ZonaHorariaOperador zona) : IConductoresPuertoEntrada
{
    private const string MensajeRequeridos =
        "nombre, telefonoWhatsApp, numeroLicencia, categoriaLicencia y vencimientoLicencia son obligatorios.";

    public async Task<ConductorRespuesta> RegistrarAsync(ConductorPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        ValidarConductor(peticion);
        return Mapeo.Conductor(await registrar.EjecutarAsync(new RegistrarConductorDominio(
            peticion.Nombre!.Trim(), TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp), Requeridos.Texto(peticion.Correo),
            peticion.NumeroLicencia!.Trim(), peticion.CategoriaLicencia!.Trim(), peticion.VencimientoLicencia!.Value, actorId, peticion.AptoServicioCritico ?? false), ct));
    }

    public async Task<ConductorRespuesta> ActualizarAsync(Guid conductorId, ConductorPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        ValidarConductor(peticion);
        return Mapeo.Conductor(await actualizar.EjecutarAsync(new ActualizarConductorDominio(
            conductorId, peticion.Nombre!.Trim(), TelefonosWhatsApp.Normalizar(peticion.TelefonoWhatsApp), Requeridos.Texto(peticion.Correo),
            peticion.NumeroLicencia!.Trim(), peticion.CategoriaLicencia!.Trim(), peticion.VencimientoLicencia!.Value, actorId, peticion.AptoServicioCritico ?? false), ct));
    }

    public async Task<ConductorRespuesta> CambiarEstadoAsync(
        Guid conductorId, EstadoConductorPeticion peticion, Guid? actorId, CancellationToken ct) =>
        Mapeo.Conductor(await cambiarEstado.EjecutarAsync(
            new CambiarEstadoConductorDominio(conductorId, ConductorDominio.ValidarEstado(peticion?.Estado), actorId), ct));

    public async Task<ConductorDetalleRespuesta> ConsultarAsync(Guid conductorId, CancellationToken ct)
    {
        var conductor = await consultar.EjecutarAsync(conductorId, ct);
        return new ConductorDetalleRespuesta(
            Mapeo.Conductor(conductor),
            conductor.Turnos.OrderBy(t => t.Inicio).Select(Mapeo.Turno).ToList(),
            conductor.Documentos.Select(d => new DocumentoConductorRespuesta(d.Id, d.Tipo.ToString(), d.FechaVencimiento)).ToList());
    }

    public async Task<PaginaRespuesta<ConductorRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct)
    {
        EstadoConductor? estado = string.IsNullOrWhiteSpace(peticion?.Estado) ? null : ConductorDominio.ValidarEstado(peticion.Estado);
        var resultado = await buscar.EjecutarAsync(new BuscarConductoresDominio(
            new FiltroConductores(Requeridos.Texto(peticion?.Texto), estado), Mapeo.Paginacion(peticion)), ct);
        return Mapeo.Pagina(resultado, Mapeo.Conductor);
    }

    public async Task<TurnoRespuesta> ProgramarTurnoAsync(Guid conductorId, TurnoPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        if (HorasLocales.Opcional(peticion?.Inicio, peticion?.ZonaHoraria, zona) is not { } inicio
            || HorasLocales.Opcional(peticion?.Fin, peticion?.ZonaHoraria, zona) is not { } fin)
            throw new FlowRideExcepcion("inicio y fin son obligatorios.");
        return Mapeo.Turno(await programarTurno.EjecutarAsync(new ProgramarTurnoDominio(conductorId, inicio, fin, actorId), ct));
    }

    public async Task<IReadOnlyList<ConductorDisponibleRespuesta>> ConsultarDisponiblesAsync(
        DateTime? inicio, DateTime? fin, string? zonaHoraria, CancellationToken ct)
    {
        if (HorasLocales.Opcional(inicio, zonaHoraria, zona) is not { } desde || HorasLocales.Opcional(fin, zonaHoraria, zona) is not { } hasta)
            throw new FlowRideExcepcion("inicio y fin son obligatorios.");
        var conductores = await disponibles.EjecutarAsync(new ConsultarDisponibilidadDominio(desde, hasta), ct);
        return conductores
            .Select(c => new ConductorDisponibleRespuesta(c.Id, c.Nombre, c.CategoriaLicencia, c.VencimientoLicencia))
            .ToList();
    }

    private static void ValidarConductor(ConductorPeticion? peticion) =>
        Requeridos.Validar(peticion, MensajeRequeridos, peticion?.Nombre, peticion?.TelefonoWhatsApp, peticion?.NumeroLicencia,
            peticion?.CategoriaLicencia, peticion?.VencimientoLicencia?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>Valida el archivo antes de tocar el almacenamiento: tipo permitido, tamaño y firma del contenido.</summary>
public sealed class DocumentosConductorAdaptadorEntrada(
    ICasoUsoConRetorno<SubirDocumentoConductorDominio, DocumentoConductor> subir,
    ICasoUsoConRetorno<DocumentoConductorRefDominio, UrlDocumento> obtenerUrl,
    ICasoUsoConRetorno<DocumentoConductorRefDominio, DocumentoConductor> eliminar) : IDocumentosConductorPuertoEntrada
{
    public const long TamanoMaximo = 5 * 1024 * 1024;

    private static readonly Dictionary<string, (string Extension, byte[] Firma)> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = (".pdf", "%PDF"u8.ToArray()),
        ["image/png"] = (".png", [0x89, 0x50, 0x4E, 0x47]),
        ["image/jpeg"] = (".jpg", [0xFF, 0xD8, 0xFF])
    };

    public async Task<DocumentoConductorRespuesta> SubirAsync(
        Guid conductorId, DocumentoPeticion peticion, ArchivoPeticion? archivo, Guid? actorId, CancellationToken ct)
    {
        if (archivo is null || archivo.Tamano == 0)
            throw new FlowRideExcepcion("archivo es obligatorio.");
        if (!Enum.TryParse<TipoDocumento>(peticion?.Tipo?.Trim(), ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo))
            throw new FlowRideExcepcion($"tipo debe ser uno de: {string.Join(", ", Enum.GetNames<TipoDocumento>())}.");
        if (archivo.Tamano > TamanoMaximo)
            throw new FlowRideExcepcion("El archivo supera el máximo de 5 MB.");
        if (!TiposPermitidos.TryGetValue(archivo.TipoContenido, out var permitido))
            throw new FlowRideExcepcion("Solo se aceptan archivos PDF, PNG o JPG.");

        // Se copia a memoria (≤ 5 MB) para comprobar que el contenido corresponde al tipo declarado.
        using var contenido = new MemoryStream();
        await archivo.Contenido.CopyToAsync(contenido, ct);
        if (contenido.Length > TamanoMaximo)
            throw new FlowRideExcepcion("El archivo supera el máximo de 5 MB.");
        if (!contenido.ToArray().AsSpan().StartsWith(permitido.Firma))
            throw new FlowRideExcepcion("El contenido del archivo no corresponde a su tipo.");
        contenido.Position = 0;

        var documento = await subir.EjecutarAsync(new SubirDocumentoConductorDominio(conductorId, tipo, peticion!.FechaVencimiento,
            "documento" + permitido.Extension, archivo.TipoContenido.ToLowerInvariant(), contenido, actorId), ct);
        return new DocumentoConductorRespuesta(documento.Id, documento.Tipo.ToString(), documento.FechaVencimiento);
    }

    public async Task<UrlDocumentoRespuesta> ObtenerUrlAsync(Guid conductorId, Guid documentoId, CancellationToken ct)
    {
        var url = await obtenerUrl.EjecutarAsync(new DocumentoConductorRefDominio(conductorId, documentoId, null), ct);
        return new UrlDocumentoRespuesta(url.Url, url.ExpiraEn);
    }

    public Task EliminarAsync(Guid conductorId, Guid documentoId, Guid? actorId, CancellationToken ct) =>
        eliminar.EjecutarAsync(new DocumentoConductorRefDominio(conductorId, documentoId, actorId), ct);
}

public sealed class VehiculosAdaptadorEntrada(
    ICasoUsoConRetorno<RegistrarVehiculoDominio, Vehiculo> registrar,
    ICasoUsoConRetorno<ActualizarVehiculoDominio, Vehiculo> actualizar,
    ICasoUsoConRetorno<CambiarEstadoVehiculoDominio, Vehiculo> cambiarEstado,
    ICasoUsoConRetorno<AsignarConductorHabitualDominio, Vehiculo> asignarConductor,
    ICasoUsoConRetorno<BuscarVehiculosDominio, PaginaResultado<Vehiculo>> buscar) : IVehiculosPuertoEntrada
{
    public async Task<VehiculoRespuesta> RegistrarAsync(VehiculoPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        ValidarVehiculo(peticion, conPlaca: true);
        return Mapeo.Vehiculo(await registrar.EjecutarAsync(new RegistrarVehiculoDominio(
            peticion.Placa!.Trim(), peticion.Marca!.Trim(), peticion.Modelo!.Trim(), peticion.Capacidad!.Value,
            peticion.VencimientoRtm!.Value, peticion.VencimientoSoat!.Value, actorId), ct));
    }

    public async Task<VehiculoRespuesta> ActualizarAsync(Guid vehiculoId, VehiculoPeticion peticion, Guid? actorId, CancellationToken ct)
    {
        ValidarVehiculo(peticion, conPlaca: false);
        return Mapeo.Vehiculo(await actualizar.EjecutarAsync(new ActualizarVehiculoDominio(
            vehiculoId, peticion.Marca!.Trim(), peticion.Modelo!.Trim(), peticion.Capacidad!.Value,
            peticion.VencimientoRtm!.Value, peticion.VencimientoSoat!.Value, actorId), ct));
    }

    public async Task<VehiculoRespuesta> CambiarEstadoAsync(Guid vehiculoId, bool activo, Guid? actorId, CancellationToken ct) =>
        Mapeo.Vehiculo(await cambiarEstado.EjecutarAsync(new CambiarEstadoVehiculoDominio(vehiculoId, activo, actorId), ct));

    public async Task<VehiculoRespuesta> AsignarConductorHabitualAsync(
        Guid vehiculoId, ConductorHabitualPeticion peticion, Guid? actorId, CancellationToken ct) =>
        Mapeo.Vehiculo(await asignarConductor.EjecutarAsync(
            new AsignarConductorHabitualDominio(vehiculoId, peticion?.ConductorId, actorId), ct));

    public async Task<PaginaRespuesta<VehiculoRespuesta>> BuscarAsync(BusquedaPeticion peticion, CancellationToken ct)
    {
        var resultado = await buscar.EjecutarAsync(new BuscarVehiculosDominio(
            new FiltroRecursos(Requeridos.Texto(peticion?.Texto), peticion?.Activo), Mapeo.Paginacion(peticion)), ct);
        return Mapeo.Pagina(resultado, Mapeo.Vehiculo);
    }

    private static void ValidarVehiculo(VehiculoPeticion? peticion, bool conPlaca)
    {
        if (peticion is null
            || (conPlaca && string.IsNullOrWhiteSpace(peticion.Placa))
            || string.IsNullOrWhiteSpace(peticion.Marca)
            || string.IsNullOrWhiteSpace(peticion.Modelo)
            || peticion.Capacidad is null
            || peticion.VencimientoRtm is null
            || peticion.VencimientoSoat is null)
        {
            throw new FlowRideExcepcion((conPlaca ? "placa, " : string.Empty) +
                                        "marca, modelo, capacidad, vencimientoRtm y vencimientoSoat son obligatorios.");
        }
    }
}

internal static class Requeridos
{
    public static void Validar([NotNull] object? peticion, string mensaje, params string?[] valores)
    {
        if (peticion is null || valores.Any(string.IsNullOrWhiteSpace))
            throw new FlowRideExcepcion(mensaje);
    }

    public static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

internal static class Mapeo
{
    public static Paginacion Paginacion(BusquedaPeticion? peticion) => new(peticion?.Pagina, peticion?.TamanoPagina);

    public static PaginaRespuesta<TDestino> Pagina<TOrigen, TDestino>(PaginaResultado<TOrigen> pagina, Func<TOrigen, TDestino> conversion) =>
        new(pagina.Elementos.Select(conversion).ToList(), pagina.Total, pagina.Paginacion.Pagina, pagina.Paginacion.Tamano);

    public static EmpresaClienteRespuesta Empresa(EmpresaCliente e) => new(e.Id, e.Nombre, e.Nit, e.CorreoContacto, e.Activa);

    public static PasajeroRespuesta Pasajero(Pasajero p) =>
        new(p.Id, p.Nombre, p.TelefonoWhatsApp, p.Correo, p.EmpresaClienteId, p.CodigoInterno, p.Activo);

    public static ConductorRespuesta Conductor(Conductor c) => new(c.Id, c.Nombre, c.TelefonoWhatsApp, c.Correo,
        c.NumeroLicencia, c.CategoriaLicencia, c.VencimientoLicencia, c.Estado.ToString(), c.Activo, c.AptoServicioCritico);

    public static TurnoRespuesta Turno(Turno t) => new(t.Id, t.Inicio, t.Fin, t.Estado.ToString(), t.InicioLocal, t.FinLocal, t.ZonaHoraria);

    public static VehiculoRespuesta Vehiculo(Vehiculo v) => new(v.Id, v.Placa, v.Marca, v.Modelo, v.Capacidad,
        v.VencimientoRtm, v.VencimientoSoat, v.Activo, v.ConductorHabitualId);
}
