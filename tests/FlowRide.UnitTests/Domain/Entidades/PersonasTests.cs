using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using static FlowRide.UnitTests.Domain.Entidades.Datos;

namespace FlowRide.UnitTests.Domain.Entidades;

public class UsuarioTests
{
    [Fact]
    public void NuevoUsuario_QuedaActivo_ConTextosNormalizados()
    {
        var usuario = new Usuario(Id(1), "  Ana  ", " +573001 ", RolId, Ahora, correo: "  ", identidadExternaId: " sub-1 ");

        Assert.True(usuario.Activo);
        Assert.Equal("Ana", usuario.Nombre);
        Assert.Equal("+573001", usuario.TelefonoWhatsApp);
        Assert.Null(usuario.Correo);
        Assert.Equal("sub-1", usuario.IdentidadExternaId);
    }

    [Fact]
    public void ActivarDesactivarYCambiarRol()
    {
        var usuario = new Usuario(Id(1), "Ana", "+57300", RolId, Ahora);

        usuario.Desactivar();
        Assert.False(usuario.Activo);
        usuario.Activar();
        Assert.True(usuario.Activo);

        usuario.CambiarRol(Id(9));
        Assert.Equal(Id(9), usuario.RolId);
        Assert.Throws<ArgumentException>(() => usuario.CambiarRol(Guid.Empty));
    }

    [Fact]
    public void ActualizarDatosYVincularIdentidadExterna()
    {
        var usuario = new Usuario(Id(1), "Ana", "+57300", RolId, Ahora);

        usuario.ActualizarDatos("Ana María", "+57311", "ana@x.co");
        usuario.VincularIdentidadExterna("kc-123");

        Assert.Equal(("Ana María", "+57311", "ana@x.co", "kc-123"),
            (usuario.Nombre, usuario.TelefonoWhatsApp, usuario.Correo, usuario.IdentidadExternaId));
    }

    [Fact]
    public void DatosObligatorios_SeValidan()
    {
        Assert.Throws<ArgumentException>(() => new Usuario(Guid.Empty, "Ana", "+57", RolId, Ahora));
        Assert.Throws<ArgumentException>(() => new Usuario(Id(1), " ", "+57", RolId, Ahora));
        Assert.Throws<ArgumentException>(() => new Usuario(Id(1), "Ana", "+57", Guid.Empty, Ahora));
    }
}

public class PasajeroTests
{
    [Fact]
    public void SolicitarViaje_CreaSolicitudPendienteDeSuEmpresa()
    {
        var pasajero = Pasajero();

        var solicitud = pasajero.SolicitarViaje(Id(30), "Origen", "Destino", Ahora.AddHours(3), TimeSpan.FromMinutes(30),
            2, CanalSolicitud.WhatsApp, Ahora, urgente: true);

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Equal(pasajero.Id, solicitud.PasajeroId);
        Assert.Equal(EmpresaId, solicitud.EmpresaClienteId);
        Assert.True(solicitud.Urgente);
    }

    [Fact]
    public void PasajeroInactivo_NoPuedeSolicitar()
    {
        var pasajero = Pasajero();
        pasajero.Desactivar();

        Assert.Throws<InvalidOperationException>(() => pasajero.SolicitarViaje(Id(30), "O", "D", Ahora,
            TimeSpan.FromMinutes(30), 1, CanalSolicitud.WhatsApp, Ahora));
    }

    [Fact]
    public void Calificar_SoloViajesFinalizados()
    {
        var pasajero = Pasajero();
        var viaje = ViajeEnCurso();

        Assert.Throws<InvalidOperationException>(() => pasajero.Calificar(Id(80), viaje, 5, null, Ahora));

        viaje.Finalizar(Ahora.AddHours(5));
        var calificacion = pasajero.Calificar(Id(80), viaje, 4, " Muy puntual ", Ahora.AddHours(6));

        Assert.Equal((viaje.Id, pasajero.Id, 4, "Muy puntual"),
            (calificacion.ViajeId, calificacion.PasajeroId, calificacion.Puntaje, calificacion.Comentario));
    }

    [Fact]
    public void RequiereCodigoYEmpresa()
    {
        Assert.Throws<ArgumentException>(() => new Pasajero(Id(10), "Ana", "+57", RolId, Ahora, " ", EmpresaId));
        Assert.Throws<ArgumentException>(() => new Pasajero(Id(10), "Ana", "+57", RolId, Ahora, "T-1", Guid.Empty));
    }
}

public class CatalogosTests
{
    [Fact]
    public void Rol_RequiereNombreYDescripcion()
    {
        var rol = new Rol(Id(1), "Conductor", "Recibe asignaciones por WhatsApp");

        Assert.Equal("Conductor", rol.Nombre);
        Assert.Throws<ArgumentException>(() => new Rol(Id(1), "", "x"));
    }

    [Fact]
    public void EmpresaCliente_ActivarDesactivarYActualizar()
    {
        var empresa = new EmpresaCliente(Id(2), "Aerolínea", "900.123", "ops@aero.co");

        empresa.Desactivar();
        Assert.False(empresa.Activa);
        empresa.Activar();
        empresa.ActualizarDatos("Aerolínea SAS", "contacto@aero.co");

        Assert.Equal(("Aerolínea SAS", "contacto@aero.co", true), (empresa.Nombre, empresa.CorreoContacto, empresa.Activa));
    }

    [Fact]
    public void Itinerario_ConservaSusDatos()
    {
        var itinerario = new Itinerario(Id(5), EmpresaId, "AV-204", Hoy, "Base", "Aeropuerto",
            new TimeOnly(4, 0), new TimeOnly(4, 45));

        Assert.Equal(("AV-204", EmpresaId), (itinerario.CodigoServicio, itinerario.EmpresaClienteId));
        Assert.Throws<ArgumentException>(() => new Itinerario(Id(5), Guid.Empty, "AV-204", Hoy, "B", "A",
            new TimeOnly(4, 0), new TimeOnly(5, 0)));
    }
}
