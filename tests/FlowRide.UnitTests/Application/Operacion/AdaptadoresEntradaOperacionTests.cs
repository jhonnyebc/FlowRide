using System.Text;
using System.Text.Json;
using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Operacion.CasosUso;
using FlowRide.Application.Operacion.Entrada.Adaptadores;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Parametros.Entrada.Adaptadores;
using FlowRide.Application.Parametros.Entrada.Dto;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.UnitTests.Application.Operacion;

public class AdaptadoresEntradaOperacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Vigente = new(2027, 12, 31);
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

    private static Grabador<TEntrada, TSalida> Falla<TEntrada, TSalida>() => new(_ => throw new InvalidOperationException("no debía llamarse"));

    private static PaginaResultado<T> Pagina<T>(params T[] elementos) => new(elementos, elementos.Length, new Paginacion(1, 10));

    private static Conductor Conductor() =>
        new(Guid.NewGuid(), "Luis", "+57310", RolesSistema.ConductorId, Ahora, "1001", "C2", Vigente);

    private static EmpresaCliente Empresa() => new(Guid.NewGuid(), "Aerolínea", "900", "ops@aero.co");

    private static Vehiculo Vehiculo() => new(Guid.NewGuid(), "abc123", "Toyota", "Hiace", 10, Vigente, Vigente);

    [Fact]
    public async Task Empresas_ValidanRecortanYPaginan()
    {
        var registrar = new Grabador<RegistrarEmpresaClienteDominio, EmpresaCliente>(_ => Empresa());
        var buscar = new Grabador<BuscarEmpresasClienteDominio, PaginaResultado<EmpresaCliente>>(_ => Pagina(Empresa()));
        var actualizar = new Grabador<ActualizarEmpresaClienteDominio, EmpresaCliente>(_ => Empresa());
        var estado = new Grabador<CambiarEstadoEmpresaClienteDominio, EmpresaCliente>(_ => Empresa());
        var adaptador = new EmpresasClienteAdaptadorEntrada(registrar, actualizar, estado, buscar);

        await adaptador.RegistrarAsync(new EmpresaClientePeticion { Nombre = " A ", Nit = " 900 ", CorreoContacto = " a@a.co " }, Actor, default);
        await adaptador.ActualizarAsync(Guid.Empty, new EmpresaClientePeticion { Nombre = "B", CorreoContacto = "b@b.co" }, Actor, default);
        await adaptador.CambiarEstadoAsync(Guid.Empty, false, Actor, default);
        var pagina = await adaptador.BuscarAsync(new BusquedaPeticion { Texto = "  ", Activo = true, Pagina = 2, TamanoPagina = 5 }, default);

        Assert.Equal(new RegistrarEmpresaClienteDominio("A", "900", "a@a.co", Actor), Assert.Single(registrar.Recibidos));
        var busqueda = Assert.Single(buscar.Recibidos);
        Assert.Null(busqueda.Filtro.Texto);
        Assert.Equal((2, 5), (busqueda.Paginacion.Pagina, busqueda.Paginacion.Tamano));
        Assert.Equal((1, 1, 10), (pagina.Total, pagina.Pagina, pagina.TamanoPagina));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(new EmpresaClientePeticion { Nombre = "A" }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ActualizarAsync(Guid.Empty, null!, Actor, default));
    }

    [Fact]
    public async Task Pasajeros_ExigenEmpresaYCodigo()
    {
        var empresaId = Guid.NewGuid();
        var pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", empresaId);
        var registrar = new Grabador<RegistrarPasajeroDominio, Pasajero>(_ => pasajero);
        var actualizar = new Grabador<ActualizarPasajeroDominio, Pasajero>(_ => pasajero);
        var buscar = new Grabador<BuscarPasajerosDominio, PaginaResultado<Pasajero>>(_ => Pagina(pasajero));
        var adaptador = new PasajerosAdaptadorEntrada(registrar, actualizar, buscar);

        var respuesta = await adaptador.RegistrarAsync(new PasajeroPeticion
        {
            Nombre = "Ana",
            TelefonoWhatsApp = "+573001112233",
            Correo = " ",
            EmpresaClienteId = empresaId,
            CodigoInterno = " E-1 "
        }, Actor, default);
        await adaptador.ActualizarAsync(pasajero.Id, new PasajeroPeticion { Nombre = "Ana", TelefonoWhatsApp = "+573001112233" }, Actor, default);
        await adaptador.BuscarAsync(new BusquedaPeticion { EmpresaClienteId = empresaId }, default);

        Assert.Null(Assert.Single(registrar.Recibidos).Correo);
        Assert.Equal("E-1", registrar.Recibidos[0].CodigoInterno);
        Assert.Equal(empresaId, respuesta.EmpresaClienteId);
        Assert.Equal(empresaId, Assert.Single(buscar.Recibidos).Filtro.EmpresaClienteId);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(
            new PasajeroPeticion { Nombre = "Ana", TelefonoWhatsApp = "+573001112233", CodigoInterno = "E-1" }, Actor, default));
    }

    [Fact]
    public async Task Conductores_ValidanEstadoFechasYMapeanElDetalle()
    {
        var conductor = Conductor();
        conductor.ProgramarTurno(Guid.NewGuid(), new FranjaHoraria(Ahora, Ahora.AddHours(8)));
        conductor.AgregarDocumento(Guid.NewGuid(), TipoDocumento.Licencia, "documents/x.pdf", Vigente);
        var registrar = new Grabador<RegistrarConductorDominio, Conductor>(_ => conductor);
        var estado = new Grabador<CambiarEstadoConductorDominio, Conductor>(_ => conductor);
        var buscar = new Grabador<BuscarConductoresDominio, PaginaResultado<Conductor>>(_ => Pagina(conductor));
        var turno = new Grabador<ProgramarTurnoDominio, Turno>(_ => conductor.Turnos[0]);
        var disponibles = new Grabador<ConsultarDisponibilidadDominio, IReadOnlyList<Conductor>>(_ => [conductor]);
        var adaptador = new ConductoresAdaptadorEntrada(registrar, new Grabador<ActualizarConductorDominio, Conductor>(_ => conductor), estado,
            new Grabador<Guid, Conductor>(_ => conductor), buscar, turno, disponibles, new Grabador<ActualizarEstadoTurnosDominio, int>(_ => 0),
            HorasDePrueba.ZonaOperador);
        var peticion = new ConductorPeticion
        {
            Nombre = "Luis",
            TelefonoWhatsApp = "+573101112233",
            NumeroLicencia = " 1001 ",
            CategoriaLicencia = "C2",
            VencimientoLicencia = Vigente
        };

        var registrado = await adaptador.RegistrarAsync(peticion, Actor, default);
        await adaptador.ActualizarAsync(conductor.Id, peticion, Actor, default);
        await adaptador.CambiarEstadoAsync(conductor.Id, new EstadoConductorPeticion { Estado = "suspendido" }, Actor, default);
        var detalle = await adaptador.ConsultarAsync(conductor.Id, default);
        await adaptador.BuscarAsync(new BusquedaPeticion { Estado = "Inactivo" }, default);
        var programado = await adaptador.ProgramarTurnoAsync(conductor.Id, new TurnoPeticion { Inicio = Ahora.HoraLocal(), Fin = Ahora.AddHours(8).HoraLocal() }, Actor, default);
        var libres = await adaptador.ConsultarDisponiblesAsync(Ahora.HoraLocal(), Ahora.AddHours(1).HoraLocal(), null, default);

        Assert.Equal("1001", Assert.Single(registrar.Recibidos).NumeroLicencia);
        Assert.Equal("Activo", registrado.Estado);
        Assert.Equal(EstadoConductor.Suspendido, Assert.Single(estado.Recibidos).Estado);
        Assert.Equal((1, 1), (detalle.Turnos.Count, detalle.Documentos.Count));
        Assert.Equal("Licencia", detalle.Documentos[0].Tipo);
        Assert.Equal(EstadoConductor.Inactivo, Assert.Single(buscar.Recibidos).Filtro.Estado);
        Assert.Equal("Programado", programado.Estado);
        Assert.Equal(conductor.Id, Assert.Single(libres).Id);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(peticion with { VencimientoLicencia = null }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            adaptador.CambiarEstadoAsync(conductor.Id, new EstadoConductorPeticion { Estado = "Feliz" }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.BuscarAsync(new BusquedaPeticion { Estado = "Feliz" }, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ProgramarTurnoAsync(conductor.Id, new TurnoPeticion { Inicio = Ahora.HoraLocal() }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ConsultarDisponiblesAsync(null, Ahora.HoraLocal(), null, default));
    }

    public static TheoryData<string, byte[], string> ArchivosValidos => new()
    {
        { "application/pdf", Encoding.ASCII.GetBytes("%PDF-1.7 ..."), ".pdf" },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D], ".png" },
        { "IMAGE/JPEG", [0xFF, 0xD8, 0xFF, 0xE0], ".jpg" }
    };

    [Theory]
    [MemberData(nameof(ArchivosValidos))]
    public async Task Documentos_AceptanPdfPngYJpgConSuFirma(string tipo, byte[] contenido, string extension)
    {
        var conductorId = Guid.NewGuid();
        var subir = new Grabador<SubirDocumentoConductorDominio, DocumentoConductor>(d =>
            new DocumentoConductor(Guid.NewGuid(), d.ConductorId, d.Tipo, "documents/x", d.FechaVencimiento));
        var adaptador = new DocumentosConductorAdaptadorEntrada(subir, Falla<DocumentoConductorRefDominio, UrlDocumento>(),
            Falla<DocumentoConductorRefDominio, DocumentoConductor>());

        var respuesta = await adaptador.SubirAsync(conductorId, new DocumentoPeticion { Tipo = "soat", FechaVencimiento = Vigente },
            new ArchivoPeticion("foto" + extension, tipo, contenido.Length, new MemoryStream(contenido)), Actor, default);

        var datos = Assert.Single(subir.Recibidos);
        Assert.Equal(("documento" + extension, tipo.ToLowerInvariant()), (datos.NombreArchivo, datos.TipoContenido));
        Assert.Equal(TipoDocumento.Soat, datos.Tipo);
        Assert.Equal("Soat", respuesta.Tipo);
    }

    public static TheoryData<string?, string, byte[], long?> ArchivosInvalidos => new()
    {
        { "Licencia", "application/pdf", [], null },
        { "Pasaporte", "application/pdf", Encoding.ASCII.GetBytes("%PDF"), null },
        { "Licencia", "application/zip", [0x50, 0x4B], null },
        { "Licencia", "application/pdf", [0x4D, 0x5A, 0x90], null },
        { "Licencia", "image/png", Encoding.ASCII.GetBytes("%PDF"), null },
        { "Licencia", "application/pdf", Encoding.ASCII.GetBytes("%PDF"), 6L * 1024 * 1024 }
    };

    [Theory]
    [MemberData(nameof(ArchivosInvalidos))]
    public async Task Documentos_RechazanTipoTamanoOFirmaIncorrectos(string? tipo, string contentType, byte[] contenido, long? tamano)
    {
        var adaptador = new DocumentosConductorAdaptadorEntrada(Falla<SubirDocumentoConductorDominio, DocumentoConductor>(),
            Falla<DocumentoConductorRefDominio, UrlDocumento>(), Falla<DocumentoConductorRefDominio, DocumentoConductor>());

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.SubirAsync(Guid.NewGuid(), new DocumentoPeticion { Tipo = tipo },
            new ArchivoPeticion("a", contentType, tamano ?? contenido.Length, new MemoryStream(contenido)), Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            adaptador.SubirAsync(Guid.NewGuid(), new DocumentoPeticion { Tipo = "Licencia" }, null, Actor, default));
    }

    [Fact]
    public async Task Documentos_DescargaYEliminacion()
    {
        var url = new Grabador<DocumentoConductorRefDominio, UrlDocumento>(_ => new UrlDocumento(new Uri("https://x/y"), Ahora));
        var eliminar = new Grabador<DocumentoConductorRefDominio, DocumentoConductor>(d =>
            new DocumentoConductor(d.DocumentoId, d.ConductorId, TipoDocumento.Otro, "documents/x", null));
        var adaptador = new DocumentosConductorAdaptadorEntrada(Falla<SubirDocumentoConductorDominio, DocumentoConductor>(), url, eliminar);

        var respuesta = await adaptador.ObtenerUrlAsync(Guid.NewGuid(), Guid.NewGuid(), default);
        await adaptador.EliminarAsync(Guid.NewGuid(), Guid.NewGuid(), Actor, default);

        Assert.Equal(new Uri("https://x/y"), respuesta.Url);
        Assert.Equal(Actor, Assert.Single(eliminar.Recibidos).ActorId);
    }

    [Fact]
    public async Task Vehiculos_ValidanYMapean()
    {
        var vehiculo = Vehiculo();
        var registrar = new Grabador<RegistrarVehiculoDominio, Vehiculo>(_ => vehiculo);
        var habitual = new Grabador<AsignarConductorHabitualDominio, Vehiculo>(_ => vehiculo);
        var adaptador = new VehiculosAdaptadorEntrada(registrar, new Grabador<ActualizarVehiculoDominio, Vehiculo>(_ => vehiculo),
            new Grabador<CambiarEstadoVehiculoDominio, Vehiculo>(_ => vehiculo), habitual,
            new Grabador<BuscarVehiculosDominio, PaginaResultado<Vehiculo>>(_ => Pagina(vehiculo)));
        var peticion = new VehiculoPeticion
        {
            Placa = "abc123",
            Marca = "Toyota",
            Modelo = "Hiace",
            Capacidad = 10,
            VencimientoRtm = Vigente,
            VencimientoSoat = Vigente
        };

        var respuesta = await adaptador.RegistrarAsync(peticion, Actor, default);
        await adaptador.ActualizarAsync(vehiculo.Id, peticion with { Placa = null }, Actor, default);
        await adaptador.CambiarEstadoAsync(vehiculo.Id, false, Actor, default);
        await adaptador.AsignarConductorHabitualAsync(vehiculo.Id, new ConductorHabitualPeticion(), Actor, default);
        var pagina = await adaptador.BuscarAsync(new BusquedaPeticion { Texto = "abc" }, default);

        Assert.Equal("ABC123", respuesta.Placa);
        Assert.Null(Assert.Single(habitual.Recibidos).ConductorId);
        Assert.Single(pagina.Elementos);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.RegistrarAsync(peticion with { Placa = " " }, Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.ActualizarAsync(vehiculo.Id, peticion with { Capacidad = null }, Actor, default));
    }

    [Fact]
    public async Task Parametros_DevuelvenJsonYExigenValor()
    {
        var definicion = CatalogoParametros.Buscar(CatalogoParametros.FranjasCriticas);
        var cambiar = new Grabador<CambiarParametroDominio, ParametroVigente>(d =>
            new ParametroVigente(definicion, new Parametro(definicion, d.ValorJson, d.ActorId, Ahora)));
        var consultar = new Grabador<ConsultarParametrosDominio, IReadOnlyList<ParametroVigente>>(_ =>
            CatalogoParametros.Todas.Select(d => new ParametroVigente(d, null)).ToList());
        var adaptador = new ParametrosAdaptadorEntrada(consultar, cambiar);

        var todos = await adaptador.ConsultarAsync(default);
        var cambiado = await adaptador.CambiarAsync(definicion.Clave,
            new CambiarParametroPeticion { Valor = JsonDocument.Parse("[\"05:00-07:00\"]").RootElement }, Actor, default);

        var entero = todos.Single(p => p.Clave == CatalogoParametros.AnticipacionRecordatorioMinutos);
        Assert.Equal((5, 1440, 60), (entero.Minimo, entero.Maximo, entero.Valor.GetInt32()));
        Assert.Null(todos.Single(p => p.Clave == CatalogoParametros.FranjasCriticas).Minimo);
        Assert.Equal(JsonValueKind.Array, cambiado.Valor.ValueKind);
        Assert.Equal((1, false), (cambiado.Version, cambiado.EsPorDefecto));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.CambiarAsync(definicion.Clave, new CambiarParametroPeticion(), Actor, default));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => adaptador.CambiarAsync(definicion.Clave,
            new CambiarParametroPeticion { Valor = JsonDocument.Parse("null").RootElement }, Actor, default));
    }
}
