using System.Collections.Concurrent;
using FlowRide.Application.Operacion.Salida.Puertos;
using FlowRide.Application.Parametros.Salida.Puertos;
using FlowRide.Application.Seguridad.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Parametros.Repositorios;
using FlowRide.Domain.ValoresObjeto;
using FlowRide.Domain.Viajes.Repositorios;

namespace FlowRide.UnitTests.Application.Operacion;

/// <summary>Dobles en memoria de los puertos de salida de Operación y Parámetros.</summary>
internal abstract class RepositorioEnMemoria<T>(Func<T, Guid> id)
{
    public Dictionary<Guid, T> Elementos { get; } = [];
    public int Modificados { get; private set; }

    public Task CrearAsync(T entidad, CancellationToken ct)
    {
        Elementos.Add(id(entidad), entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid clave, T entidad, CancellationToken ct)
    {
        Elementos[clave] = entidad;
        Modificados++;
        return Task.CompletedTask;
    }

    public Task<T?> ConsultarPorIdAsync(Guid clave, CancellationToken ct) => Task.FromResult(Elementos.GetValueOrDefault(clave));

    public Task<IReadOnlyList<T>> ConsultarTodosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<T>>(Elementos.Values.ToList());

    protected static PaginaResultado<T> Paginar(IEnumerable<T> elementos, Paginacion paginacion)
    {
        var lista = elementos.ToList();
        return new PaginaResultado<T>(lista.Skip(paginacion.Salto).Take(paginacion.Tamano).ToList(), lista.Count, paginacion);
    }
}

internal sealed class EmpresasEnMemoria() : RepositorioEnMemoria<EmpresaCliente>(e => e.Id), IEmpresaClienteRepositorio
{
    public Task<EmpresaCliente?> ConsultarPorNitAsync(string nit, CancellationToken ct) =>
        Task.FromResult(Elementos.Values.FirstOrDefault(e => e.Nit == nit));

    public Task<PaginaResultado<EmpresaCliente>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct) =>
        Task.FromResult(Paginar(Elementos.Values.Where(e => filtro.Activo is null || e.Activa == filtro.Activo), paginacion));

    public EmpresaCliente Registrar(bool activa = true)
    {
        var empresa = new EmpresaCliente(Guid.NewGuid(), "Aerolínea", $"nit-{Guid.NewGuid():N}", "ops@aero.co", activa);
        Elementos[empresa.Id] = empresa;
        return empresa;
    }
}

internal sealed class PasajerosEnMemoria() : RepositorioEnMemoria<Pasajero>(p => p.Id), IPasajeroRepositorio
{
    public Task<Pasajero?> ConsultarPorCodigoAsync(Guid empresaClienteId, string codigoInterno, CancellationToken ct) =>
        Task.FromResult(Elementos.Values.FirstOrDefault(p => p.EmpresaClienteId == empresaClienteId && p.CodigoInterno == codigoInterno));

    public Task<PaginaResultado<Pasajero>> BuscarAsync(FiltroPasajeros filtro, Paginacion paginacion, CancellationToken ct) =>
        Task.FromResult(Paginar(Elementos.Values.Where(p => filtro.EmpresaClienteId is null || p.EmpresaClienteId == filtro.EmpresaClienteId), paginacion));
}

internal sealed class ConductoresEnMemoria() : RepositorioEnMemoria<Conductor>(c => c.Id), IConductorRepositorio
{
    public Task<Conductor?> ConsultarPorLicenciaAsync(string numeroLicencia, CancellationToken ct) =>
        Task.FromResult(Elementos.Values.FirstOrDefault(c => c.NumeroLicencia == numeroLicencia));

    public Task<PaginaResultado<Conductor>> BuscarAsync(FiltroConductores filtro, Paginacion paginacion, CancellationToken ct) =>
        Task.FromResult(Paginar(Elementos.Values.Where(c => filtro.Estado is null || c.Estado == filtro.Estado), paginacion));

    public Task<IReadOnlyList<Conductor>> ConsultarConTurnoQueCubreAsync(FranjaHoraria franja, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Conductor>>(Elementos.Values.Where(c => c.TieneTurnoQueCubre(franja)).ToList());
}

internal sealed class VehiculosEnMemoria() : RepositorioEnMemoria<Vehiculo>(v => v.Id), IVehiculoRepositorio
{
    public Task<Vehiculo?> ConsultarPorPlacaAsync(string placa, CancellationToken ct) =>
        Task.FromResult(Elementos.Values.FirstOrDefault(v => v.Placa == placa));

    public Task<Vehiculo?> ConsultarPorConductorHabitualAsync(Guid conductorId, CancellationToken ct) =>
        Task.FromResult(Elementos.Values.FirstOrDefault(v => v.ConductorHabitualId == conductorId));

    public Task<PaginaResultado<Vehiculo>> BuscarAsync(FiltroRecursos filtro, Paginacion paginacion, CancellationToken ct) =>
        Task.FromResult(Paginar(Elementos.Values, paginacion));
}

internal sealed class AsignacionesEnMemoria : IAsignacionRepositorio
{
    public HashSet<Guid> Ocupados { get; } = [];
    public Dictionary<Guid, Asignacion> VigentesPorSolicitud { get; } = [];

    public Task<Asignacion?> ConsultarVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult(VigentesPorSolicitud.GetValueOrDefault(solicitudId)
                        ?? Todas.Values.FirstOrDefault(a => a.SolicitudId == solicitudId && a.EstaVigente));

    public Dictionary<Guid, Asignacion> Todas { get; } = [];
    public HashSet<Guid> VehiculosOcupados { get; } = [];
    public Dictionary<Guid, int> Recientes { get; } = [];

    public Task CrearAsync(Asignacion asignacion, CancellationToken ct)
    {
        Todas.Add(asignacion.Id, asignacion);
        return Task.CompletedTask;
    }

    public Task<Asignacion?> ConsultarPorIdAsync(Guid asignacionId, CancellationToken ct) => Task.FromResult(Todas.GetValueOrDefault(asignacionId));

    public Task<IReadOnlySet<Guid>> ConsultarVehiculosOcupadosAsync(FranjaHoraria franja, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(VehiculosOcupados);

    public Task<IReadOnlyDictionary<Guid, int>> ContarRecientesAsync(IReadOnlyCollection<Guid> conductorIds, DateTimeOffset desde, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Recientes);

    public Task<IReadOnlyList<Asignacion>> ConsultarVigentesDelConductorAsync(Guid conductorId, FranjaHoraria franja, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Asignacion>>(Todas.Values.Where(a => a.ConductorId == conductorId && a.EstaVigente
                                                                          && a.Franja.SeSolapaCon(franja)).ToList());

    public Task<IReadOnlySet<Guid>> ConsultarConductoresOcupadosAsync(FranjaHoraria franja, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(Ocupados);

    public Task<IReadOnlySet<Guid>> ConsultarConductoresQueRechazaronAsync(Guid solicitudId, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<Guid>>(Todas.Values.Where(a => a.SolicitudId == solicitudId && a.Estado == EstadoAsignacion.Rechazada)
            .Select(a => a.ConductorId).ToHashSet());

    public Task<IReadOnlyList<Guid>> ConsultarSinRespuestaAsync(DateTimeOffset asignadasAntesDe, int maximo, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>(Todas.Values.Where(a => a.Estado == EstadoAsignacion.Asignada && a.AsignadaEn <= asignadasAntesDe)
            .OrderBy(a => a.AsignadaEn).Select(a => a.Id).Take(maximo).ToList());
}

internal sealed class AlmacenEnMemoria : IAlmacenObjetos
{
    public ConcurrentDictionary<string, (byte[] Contenido, string Tipo)> Objetos { get; } = new(StringComparer.Ordinal);

    public async Task GuardarAsync(string ruta, Stream contenido, string tipoContenido, CancellationToken ct)
    {
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, ct);
        Objetos[ruta] = (copia.ToArray(), tipoContenido);
    }

    public Task<Uri> UrlDescargaAsync(string ruta, TimeSpan vigencia, CancellationToken ct) =>
        Task.FromResult(new Uri($"https://objetos.pruebas/{ruta}?vence={vigencia.TotalMinutes}"));

    public Task EliminarAsync(string ruta, CancellationToken ct)
    {
        Objetos.TryRemove(ruta, out _);
        return Task.CompletedTask;
    }
}

internal sealed class ParametrosEnMemoria : IParametroRepositorio, ICacheParametros
{
    public Dictionary<string, Parametro> Parametros { get; } = new(StringComparer.Ordinal);
    public int Invalidaciones { get; private set; }

    public Task CrearAsync(Parametro entidad, CancellationToken ct)
    {
        Parametros.Add(entidad.Clave, entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(string id, Parametro entidad, CancellationToken ct)
    {
        Parametros[id] = entidad;
        return Task.CompletedTask;
    }

    public Task<Parametro?> ConsultarPorIdAsync(string id, CancellationToken ct) => Task.FromResult(Parametros.GetValueOrDefault(id));

    public Task<IReadOnlyList<Parametro>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Parametro>>(Parametros.Values.ToList());

    public void Invalidar() => Invalidaciones++;
}

internal sealed class RecifradorEnMemoria(int reescritos) : IRecifradorCampos
{
    public Task<int> RecifrarAsync(CancellationToken ct) => Task.FromResult(reescritos);
}

internal sealed class ViajesEnMemoria : IViajeRepositorio
{
    public Dictionary<Guid, Viaje> Todos { get; } = [];

    public Task CrearAsync(Viaje entidad, CancellationToken ct)
    {
        Todos.Add(entidad.Id, entidad);
        return Task.CompletedTask;
    }

    public Task<Viaje?> ConsultarPorIdAsync(Guid viajeId, CancellationToken ct) => Task.FromResult(Todos.GetValueOrDefault(viajeId));

    public Task<Viaje?> ConsultarPorAsignacionAsync(Guid asignacionId, CancellationToken ct) =>
        Task.FromResult(Todos.Values.SingleOrDefault(v => v.AsignacionId == asignacionId));
}

internal sealed class CalificacionesEnMemoria : ICalificacionRepositorio
{
    public List<Calificacion> Todas { get; } = [];

    public Task CrearAsync(Calificacion entidad, CancellationToken ct)
    {
        Todas.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<bool> ExisteAsync(Guid viajeId, Guid pasajeroId, CancellationToken ct) =>
        Task.FromResult(Todas.Any(c => c.ViajeId == viajeId && c.PasajeroId == pasajeroId));
}
