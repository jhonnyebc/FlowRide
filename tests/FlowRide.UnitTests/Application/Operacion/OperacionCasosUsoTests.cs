using System.Text;
using FlowRide.Application.Operacion.CasosUso;
using FlowRide.Application.Parametros.CasosUso;
using FlowRide.Application.Seguridad.CasosUso;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Operacion;
using FlowRide.Domain.Operacion.GestionRecursos;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Identidad;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Operacion;

public abstract class CasosUsoOperacionBase
{
    protected static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);
    protected static readonly Guid Actor = Guid.NewGuid();
    protected static readonly DateOnly Vigente = new(2027, 12, 31);

    internal UsuariosEnMemoria Usuarios { get; } = new();
    internal EmpresasEnMemoria Empresas { get; } = new();
    internal PasajerosEnMemoria Pasajeros { get; } = new();
    internal ConductoresEnMemoria Conductores { get; } = new();
    internal VehiculosEnMemoria Vehiculos { get; } = new();
    internal AsignacionesEnMemoria Asignaciones { get; } = new();
    internal ViajesEnMemoria Viajes { get; } = new();
    internal AlmacenEnMemoria Almacen { get; } = new();
    internal AuditoriaEnMemoria Auditoria { get; } = new();
    internal UnidadTrabajoEnMemoria UnidadTrabajo { get; }
    internal RelojFijo Reloj { get; } = new(Ahora);

    protected CasosUsoOperacionBase() => UnidadTrabajo = new UnidadTrabajoEnMemoria(Auditoria);

    internal UsuarioDominio UsuarioDominio => new(Usuarios);
    internal ConductorDominio ConductorDominio => new(Conductores);

    internal Conductor ConductorRegistrado(string licencia = "1001", EstadoConductor estado = EstadoConductor.Activo)
    {
        var conductor = new Conductor(Guid.NewGuid(), $"Conductor {licencia}", $"+57{licencia}", RolesSistema.ConductorId, Ahora,
            licencia, "C2", Vigente, estado: estado);
        Conductores.Elementos[conductor.Id] = conductor;
        return conductor;
    }

    internal string UltimaAccion => Assert.Single(Auditoria.Eventos.TakeLast(1)).Accion;
}

public class EmpresasYPasajerosCasosUsoTests : CasosUsoOperacionBase
{
    [Fact]
    public async Task RegistrarEmpresa_LaGuardaYLaAudita()
    {
        var empresa = await new RegistrarEmpresaClienteCasoUso(new EmpresaClienteDominio(Empresas), Empresas, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new RegistrarEmpresaClienteDominio("Aerolínea", "900-1", "ops@aero.co", Actor), CancellationToken.None);

        Assert.True(empresa.Activa);
        Assert.Same(empresa, Empresas.Elementos[empresa.Id]);
        Assert.Equal("empresa_cliente.crear", UltimaAccion);
    }

    [Fact]
    public async Task RegistrarEmpresa_NitRepetidoOCorreoInvalido_SeRechaza()
    {
        var existente = Empresas.Registrar();
        var casoUso = new RegistrarEmpresaClienteCasoUso(new EmpresaClienteDominio(Empresas), Empresas, Auditoria, UnidadTrabajo, Reloj);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarEmpresaClienteDominio("Otra", existente.Nit, "a@a.co", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarEmpresaClienteDominio("Otra", "900-2", "correo", Actor), CancellationToken.None));
        Assert.Empty(Auditoria.Eventos);
    }

    [Fact]
    public async Task ActualizarYCambiarEstadoDeEmpresa()
    {
        var empresa = Empresas.Registrar();
        var dominio = new EmpresaClienteDominio(Empresas);

        await new ActualizarEmpresaClienteCasoUso(dominio, Empresas, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ActualizarEmpresaClienteDominio(empresa.Id, "Minera", "m@m.co", Actor), CancellationToken.None);
        var estado = new CambiarEstadoEmpresaClienteCasoUso(dominio, Empresas, Auditoria, UnidadTrabajo, Reloj);
        await estado.EjecutarAsync(new CambiarEstadoEmpresaClienteDominio(empresa.Id, false, Actor), CancellationToken.None);
        await estado.EjecutarAsync(new CambiarEstadoEmpresaClienteDominio(empresa.Id, false, Actor), CancellationToken.None);

        Assert.Equal("Minera", empresa.Nombre);
        Assert.False(empresa.Activa);
        Assert.Equal(["empresa_cliente.actualizar", "empresa_cliente.desactivar"], Auditoria.Eventos.Select(e => e.Accion));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            estado.EjecutarAsync(new CambiarEstadoEmpresaClienteDominio(Guid.NewGuid(), true, Actor), CancellationToken.None));
    }

    [Fact]
    public async Task RegistrarPasajero_EnEmpresaActivaConCodigoUnico()
    {
        var empresa = Empresas.Registrar();
        var casoUso = new RegistrarPasajeroCasoUso(UsuarioDominio, new EmpresaClienteDominio(Empresas), new PasajeroDominio(Pasajeros),
            Pasajeros, Auditoria, UnidadTrabajo, Reloj);

        var pasajero = await casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Ana", "+57300", "ana@aero.co", empresa.Id, "E-1", Actor), CancellationToken.None);

        Assert.Equal(RolesSistema.PasajeroId, pasajero.RolId);
        Assert.Null(pasajero.IdentidadExternaId);
        Assert.Equal("pasajero.crear", UltimaAccion);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Beto", "+57301", null, empresa.Id, "E-1", Actor), CancellationToken.None));
    }

    [Fact]
    public async Task RegistrarPasajero_EmpresaInactivaTelefonoRepetidoOCorreoInvalido_SeRechaza()
    {
        var activa = Empresas.Registrar();
        var inactiva = Empresas.Registrar(activa: false);
        Usuarios.Registrar("Existente", "existe@a.co", "+57999");
        var casoUso = new RegistrarPasajeroCasoUso(UsuarioDominio, new EmpresaClienteDominio(Empresas), new PasajeroDominio(Pasajeros),
            Pasajeros, Auditoria, UnidadTrabajo, Reloj);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Ana", "+57300", null, inactiva.Id, "E-1", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Ana", "+57999", null, activa.Id, "E-1", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Ana", "+57300", "existe@a.co", activa.Id, "E-1", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new RegistrarPasajeroDominio("Ana", "+57300", "correo", activa.Id, "E-1", Actor), CancellationToken.None));
        Assert.Empty(Pasajeros.Elementos);
    }

    [Fact]
    public async Task ActualizarPasajero_PuedeConservarSuPropioTelefono()
    {
        var empresa = Empresas.Registrar();
        var pasajero = new Pasajero(Guid.NewGuid(), "Ana", "+57300", RolesSistema.PasajeroId, Ahora, "E-1", empresa.Id);
        Pasajeros.Elementos[pasajero.Id] = pasajero;
        Usuarios.Usuarios[pasajero.Id] = pasajero;
        var casoUso = new ActualizarPasajeroCasoUso(UsuarioDominio, new PasajeroDominio(Pasajeros), Pasajeros, Auditoria, UnidadTrabajo, Reloj);

        await casoUso.EjecutarAsync(new ActualizarPasajeroDominio(pasajero.Id, "Ana María", "+57300", "ana@a.co", Actor), CancellationToken.None);

        Assert.Equal(("Ana María", "ana@a.co"), (pasajero.Nombre, pasajero.Correo));
        Assert.Equal("pasajero.actualizar", UltimaAccion);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ActualizarPasajeroDominio(Guid.NewGuid(), "X", "+1", null, Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Buscar_DelegaEnElRepositorio()
    {
        var empresa = Empresas.Registrar();
        Empresas.Registrar(activa: false);

        var empresas = await new BuscarEmpresasClienteCasoUso(Empresas)
            .EjecutarAsync(new BuscarEmpresasClienteDominio(new FiltroRecursos(Activo: true), Paginacion.PorDefecto), CancellationToken.None);
        var pasajeros = await new BuscarPasajerosCasoUso(Pasajeros)
            .EjecutarAsync(new BuscarPasajerosDominio(new FiltroPasajeros(EmpresaClienteId: empresa.Id), Paginacion.PorDefecto), CancellationToken.None);

        Assert.Equal(empresa.Id, Assert.Single(empresas.Elementos).Id);
        Assert.Equal(0, pasajeros.Total);
    }
}

public class ConductoresCasosUsoTests : CasosUsoOperacionBase
{
    private static RegistrarConductorDominio Registro(string licencia = "1001", string telefono = "+57310", string? correo = null) =>
        new("Luis Pérez", telefono, correo, licencia, "C2", Vigente, Actor);

    private RegistrarConductorCasoUso Registrar() =>
        new(UsuarioDominio, ConductorDominio, Conductores, Auditoria, UnidadTrabajo, Reloj);

    [Fact]
    public async Task Registrar_CreaElConductorActivoConRolConductor()
    {
        var conductor = await Registrar().EjecutarAsync(Registro(correo: "luis@ops.co"), CancellationToken.None);

        Assert.Equal(RolesSistema.ConductorId, conductor.RolId);
        Assert.Equal(EstadoConductor.Activo, conductor.Estado);
        Assert.Equal("conductor.crear", UltimaAccion);
        Assert.DoesNotContain("1001", Auditoria.Eventos[0].ValoresDespues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registrar_LicenciaTelefonoOCorreoRepetidos_SeRechaza()
    {
        ConductorRegistrado("1001");
        Usuarios.Registrar("Ana", "ana@a.co", "+57999");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Registro("1001"), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Registro("2002", "+57999"), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Registro("2002", correo: "ana@a.co"), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Registrar().EjecutarAsync(Registro("2002", correo: "x"), CancellationToken.None));
    }

    [Fact]
    public async Task Actualizar_CambiaDatosYLicencia()
    {
        var conductor = ConductorRegistrado("1001");
        ConductorRegistrado("3003");
        var casoUso = new ActualizarConductorCasoUso(UsuarioDominio, ConductorDominio, Conductores, Auditoria, UnidadTrabajo, Reloj);

        await casoUso.EjecutarAsync(new ActualizarConductorDominio(conductor.Id, "Luis", "+57311", "l@o.co", "1001", "C3",
            Vigente.AddYears(1), Actor), CancellationToken.None);

        Assert.Equal(("C3", "+57311"), (conductor.CategoriaLicencia, conductor.TelefonoWhatsApp));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(new ActualizarConductorDominio(conductor.Id, "Luis",
            "+57311", null, "3003", "C3", Vigente, Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(new ActualizarConductorDominio(conductor.Id, "Luis",
            "+57311", "malo", "1001", "C3", Vigente, Actor), CancellationToken.None));
    }

    [Fact]
    public async Task CambiarEstado_AuditaAntesYDespues_YNoRepite()
    {
        var conductor = ConductorRegistrado();
        var casoUso = new CambiarEstadoConductorCasoUso(ConductorDominio, Conductores, Auditoria, UnidadTrabajo, Reloj);

        await casoUso.EjecutarAsync(new CambiarEstadoConductorDominio(conductor.Id, EstadoConductor.Suspendido, Actor), CancellationToken.None);
        await casoUso.EjecutarAsync(new CambiarEstadoConductorDominio(conductor.Id, EstadoConductor.Suspendido, Actor), CancellationToken.None);

        var evento = Assert.Single(Auditoria.Eventos);
        Assert.Equal("conductor.cambiar_estado", evento.Accion);
        Assert.Contains("Suspendido", evento.ValoresDespues, StringComparison.Ordinal);
        Assert.False(conductor.EstaActivo);
    }

    [Fact]
    public async Task ProgramarTurno_SinSolaparse()
    {
        var conductor = ConductorRegistrado();
        var casoUso = new ProgramarTurnoCasoUso(ConductorDominio, Conductores, Auditoria, UnidadTrabajo, Reloj);

        var turno = await casoUso.EjecutarAsync(new ProgramarTurnoDominio(conductor.Id, Ahora.Local(), Ahora.AddHours(8).Local(), Actor), CancellationToken.None);

        Assert.Equal(EstadoTurno.Programado, turno.Estado);
        Assert.Equal("turno.programar", UltimaAccion);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ProgramarTurnoDominio(conductor.Id, Ahora.AddHours(4).Local(), Ahora.AddHours(10).Local(), Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new ProgramarTurnoDominio(conductor.Id, Ahora.Local(), Ahora.Local(), Actor), CancellationToken.None));
    }

    [Fact]
    public async Task Disponibilidad_SoloActivosConLicenciaTurnoYLibres()
    {
        var franja = (Inicio: Ahora.AddHours(2), Fin: Ahora.AddHours(3));
        var disponible = ConductorRegistrado("1");
        var ocupado = ConductorRegistrado("2");
        var suspendido = ConductorRegistrado("3", EstadoConductor.Suspendido);
        var sinLicencia = new Conductor(Guid.NewGuid(), "Vencido", "+574", RolesSistema.ConductorId, Ahora, "4", "C2", new DateOnly(2026, 1, 1));
        Conductores.Elementos[sinLicencia.Id] = sinLicencia;
        ConductorRegistrado("5"); // sin turno
        foreach (var c in new[] { disponible, ocupado, suspendido, sinLicencia })
            c.ProgramarTurno(Guid.NewGuid(), new FlowRide.Domain.ValoresObjeto.FranjaHoraria(Ahora, Ahora.AddHours(8)));
        Asignaciones.Ocupados.Add(ocupado.Id);

        var resultado = await new ConsultarDisponibilidadCasoUso(Conductores, Asignaciones)
            .EjecutarAsync(new ConsultarDisponibilidadDominio(franja.Inicio.Local(), franja.Fin.Local()), CancellationToken.None);

        Assert.Equal(disponible.Id, Assert.Single(resultado).Id);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ConsultarDisponibilidadCasoUso(Conductores, Asignaciones)
            .EjecutarAsync(new ConsultarDisponibilidadDominio(franja.Fin.Local(), franja.Inicio.Local()), CancellationToken.None));
    }

    [Fact]
    public async Task ConsultarYBuscar()
    {
        var conductor = ConductorRegistrado();
        ConductorRegistrado("2", EstadoConductor.Inactivo);

        Assert.Same(conductor, await new ConsultarConductorCasoUso(ConductorDominio).EjecutarAsync(conductor.Id, CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ConsultarConductorCasoUso(ConductorDominio).EjecutarAsync(Guid.NewGuid(), CancellationToken.None));
        var inactivos = await new BuscarConductoresCasoUso(Conductores).EjecutarAsync(
            new BuscarConductoresDominio(new FiltroConductores(Estado: EstadoConductor.Inactivo), Paginacion.PorDefecto), CancellationToken.None);
        Assert.Equal(1, inactivos.Total);
    }

    [Fact]
    public async Task Documentos_SubirObtenerUrlYEliminar()
    {
        var conductor = ConductorRegistrado();
        var subir = new SubirDocumentoConductorCasoUso(ConductorDominio, Conductores, Almacen, Auditoria, UnidadTrabajo, Reloj);
        using var contenido = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7"));

        var documento = await subir.EjecutarAsync(new SubirDocumentoConductorDominio(conductor.Id, TipoDocumento.Licencia, Vigente,
            "documento.pdf", "application/pdf", contenido, Actor), CancellationToken.None);
        var url = await new ObtenerUrlDocumentoConductorCasoUso(ConductorDominio, Almacen, Reloj)
            .EjecutarAsync(new DocumentoConductorRefDominio(conductor.Id, documento.Id, null), CancellationToken.None);

        Assert.StartsWith($"documents/{conductor.Id}/{documento.Id}", documento.ReferenciaBlob, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", documento.ReferenciaBlob, StringComparison.Ordinal);
        Assert.True(Almacen.Objetos.ContainsKey(documento.ReferenciaBlob));
        Assert.Equal(Ahora.AddMinutes(15), url.ExpiraEn);
        Assert.Contains("vence=15", url.Url.Query, StringComparison.Ordinal);

        await new EliminarDocumentoConductorCasoUso(ConductorDominio, Conductores, Almacen, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new DocumentoConductorRefDominio(conductor.Id, documento.Id, Actor), CancellationToken.None);

        Assert.Empty(conductor.Documentos);
        Assert.Empty(Almacen.Objetos);
        Assert.Equal(["documento_conductor.crear", "documento_conductor.eliminar"], Auditoria.Eventos.Select(e => e.Accion));
    }

    [Fact]
    public async Task Documentos_SiFallaLaBase_SeBorraElArchivo_YDocumentoAjenoSeRechaza()
    {
        var conductor = ConductorRegistrado();
        UnidadTrabajo.Falla = true;
        using var contenido = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubirDocumentoConductorCasoUso(ConductorDominio, Conductores, Almacen, Auditoria, UnidadTrabajo, Reloj)
                .EjecutarAsync(new SubirDocumentoConductorDominio(conductor.Id, TipoDocumento.Soat, null, "a.png", "image/png", contenido, Actor),
                    CancellationToken.None));

        Assert.Empty(Almacen.Objetos);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ObtenerUrlDocumentoConductorCasoUso(ConductorDominio, Almacen, Reloj)
            .EjecutarAsync(new DocumentoConductorRefDominio(conductor.Id, Guid.NewGuid(), null), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            new EliminarDocumentoConductorCasoUso(ConductorDominio, Conductores, Almacen, Auditoria, UnidadTrabajo, Reloj)
                .EjecutarAsync(new DocumentoConductorRefDominio(conductor.Id, Guid.NewGuid(), Actor), CancellationToken.None));
    }
}

public class VehiculosCasosUsoTests : CasosUsoOperacionBase
{
    private VehiculoDominio VehiculoDominio => new(Vehiculos);

    private static RegistrarVehiculoDominio Registro(string placa = "abc123", int capacidad = 10) =>
        new(placa, "Toyota", "Hiace", capacidad, Vigente, Vigente, Actor);

    [Fact]
    public async Task Registrar_PlacaEnMayusculasYUnica()
    {
        var casoUso = new RegistrarVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj);

        var vehiculo = await casoUso.EjecutarAsync(Registro(), CancellationToken.None);

        Assert.Equal("ABC123", vehiculo.Placa);
        Assert.Equal("vehiculo.crear", UltimaAccion);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(Registro(" abc123 "), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(Registro("XYZ999", 0), CancellationToken.None));
    }

    [Fact]
    public async Task ActualizarYCambiarEstado()
    {
        var vehiculo = await new RegistrarVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(Registro(), CancellationToken.None);

        await new ActualizarVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj).EjecutarAsync(
            new ActualizarVehiculoDominio(vehiculo.Id, "Renault", "Master", 15, Vigente.AddYears(1), Vigente.AddYears(1), Actor), CancellationToken.None);
        var estado = new CambiarEstadoVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj);
        await estado.EjecutarAsync(new CambiarEstadoVehiculoDominio(vehiculo.Id, false, Actor), CancellationToken.None);
        await estado.EjecutarAsync(new CambiarEstadoVehiculoDominio(vehiculo.Id, false, Actor), CancellationToken.None);
        await estado.EjecutarAsync(new CambiarEstadoVehiculoDominio(vehiculo.Id, true, Actor), CancellationToken.None);

        Assert.Equal((15, true), (vehiculo.Capacidad, vehiculo.Activo));
        Assert.Equal(["vehiculo.crear", "vehiculo.actualizar", "vehiculo.desactivar", "vehiculo.activar"], Auditoria.Eventos.Select(e => e.Accion));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => new ActualizarVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj)
            .EjecutarAsync(new ActualizarVehiculoDominio(vehiculo.Id, "R", "M", -1, Vigente, Vigente, Actor), CancellationToken.None));
    }

    [Fact]
    public async Task ConductorHabitual_UnoPorVehiculoYSePuedeQuitar()
    {
        var casoUso = new AsignarConductorHabitualCasoUso(VehiculoDominio, ConductorDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj);
        var registrar = new RegistrarVehiculoCasoUso(VehiculoDominio, Vehiculos, Auditoria, UnidadTrabajo, Reloj);
        var primero = await registrar.EjecutarAsync(Registro("AAA111"), CancellationToken.None);
        var segundo = await registrar.EjecutarAsync(Registro("BBB222"), CancellationToken.None);
        var conductor = ConductorRegistrado();

        await casoUso.EjecutarAsync(new AsignarConductorHabitualDominio(primero.Id, conductor.Id, Actor), CancellationToken.None);
        await casoUso.EjecutarAsync(new AsignarConductorHabitualDominio(primero.Id, conductor.Id, Actor), CancellationToken.None);

        Assert.Equal(conductor.Id, primero.ConductorHabitualId);
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AsignarConductorHabitualDominio(segundo.Id, conductor.Id, Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => casoUso.EjecutarAsync(
            new AsignarConductorHabitualDominio(segundo.Id, Guid.NewGuid(), Actor), CancellationToken.None));

        await casoUso.EjecutarAsync(new AsignarConductorHabitualDominio(primero.Id, null, Actor), CancellationToken.None);
        Assert.Null(primero.ConductorHabitualId);
        Assert.Equal(2, Auditoria.Eventos.Count(e => e.Accion == "vehiculo.asignar_conductor_habitual"));
        Assert.Equal(2, (await new BuscarVehiculosCasoUso(Vehiculos)
            .EjecutarAsync(new BuscarVehiculosDominio(new FiltroRecursos(), Paginacion.PorDefecto), CancellationToken.None)).Total);
    }
}

public class ParametrosYSeguridadCasosUsoTests : CasosUsoOperacionBase
{
    private readonly ParametrosEnMemoria _parametros = new();

    private CambiarParametroCasoUso Cambiar() => new(_parametros, _parametros, Auditoria, UnidadTrabajo, Reloj);

    [Fact]
    public async Task Consultar_SinCambiosDevuelveLosValoresPorDefecto()
    {
        var vigentes = await new ConsultarParametrosCasoUso(_parametros).EjecutarAsync(ConsultarParametrosDominio.Todos, CancellationToken.None);

        Assert.Equal(CatalogoParametros.Todas.Count, vigentes.Count);
        Assert.All(vigentes, vigente => Assert.True(vigente.EsPorDefecto));
    }

    [Fact]
    public async Task Cambiar_CreaLaVersion1_LuegoSube_AuditaEInvalidaLaCache()
    {
        var clave = CatalogoParametros.VentanaCancelacionMinutos;

        var primero = (await Cambiar().EjecutarAsync(new CambiarParametroDominio(clave, "45", Actor), CancellationToken.None)).Version;
        var segundo = (await Cambiar().EjecutarAsync(new CambiarParametroDominio(clave, "15", Actor), CancellationToken.None)).Version;
        var igual = (await Cambiar().EjecutarAsync(new CambiarParametroDominio(clave, "15", Actor), CancellationToken.None)).Version;

        Assert.Equal((1, 2, 2), (primero, segundo, igual));
        Assert.Equal(2, _parametros.Invalidaciones);
        Assert.Equal(2, Auditoria.Eventos.Count);
        Assert.Equal("parametro.actualizar", Auditoria.Eventos[0].Accion);
        Assert.Equal(clave, Auditoria.Eventos[0].RecursoId);
        Assert.Contains("\"valor\":\"30\"", Auditoria.Eventos[0].ValoresAntes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cambiar_ValorInvalidoOClaveDesconocida_NoGuardaNada()
    {
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Cambiar().EjecutarAsync(new CambiarParametroDominio(CatalogoParametros.VentanaCancelacionMinutos, "5000", Actor), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Cambiar().EjecutarAsync(new CambiarParametroDominio("no.existe", "1", Actor), CancellationToken.None));

        Assert.Empty(_parametros.Parametros);
        Assert.Equal(0, _parametros.Invalidaciones);
    }

    [Fact]
    public async Task AsegurarCifrado_DevuelveLoReescrito() =>
        Assert.Equal(3, await new AsegurarCifradoCasoUso(new RecifradorEnMemoria(3)).EjecutarAsync(AsegurarCifradoDominio.Silo, CancellationToken.None));
}
