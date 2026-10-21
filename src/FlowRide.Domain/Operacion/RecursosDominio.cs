using FlowRide.Domain.Entidades;
using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.Operacion.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Operacion;

// Reglas de los recursos operativos que necesitan consultar el Silo. Las que dependen solo de la entidad viven en ella.

public sealed class EmpresaClienteDominio(IEmpresaClienteRepositorio empresas)
{
    public async Task<EmpresaCliente> ValidarExistaAsync(Guid empresaId, CancellationToken ct) =>
        await empresas.ConsultarPorIdAsync(empresaId, ct)
        ?? throw new FlowRideExcepcion($"La empresa cliente {empresaId} no existe.");

    /// <summary>Un pasajero nuevo solo puede pertenecer a una empresa cliente activa.</summary>
    public async Task<EmpresaCliente> ValidarActivaAsync(Guid empresaId, CancellationToken ct)
    {
        var empresa = await ValidarExistaAsync(empresaId, ct);
        if (!empresa.Activa)
            throw new FlowRideExcepcion($"La empresa cliente {empresa.Nombre} está inactiva.");
        return empresa;
    }

    public async Task ValidarNitDisponibleAsync(string nit, CancellationToken ct)
    {
        if (await empresas.ConsultarPorNitAsync(nit.Trim(), ct) is { } titular)
            throw new FlowRideExcepcion($"El NIT {nit.Trim()} ya está registrado para la empresa {titular.Nombre}.");
    }
}

public sealed class PasajeroDominio(IPasajeroRepositorio pasajeros)
{
    public async Task<Pasajero> ValidarExistaAsync(Guid pasajeroId, CancellationToken ct) =>
        await pasajeros.ConsultarPorIdAsync(pasajeroId, ct)
        ?? throw new FlowRideExcepcion($"El pasajero {pasajeroId} no existe.");

    /// <summary>El código interno identifica al empleado dentro de su empresa cliente.</summary>
    public async Task ValidarCodigoDisponibleAsync(Guid empresaClienteId, string codigoInterno, CancellationToken ct)
    {
        if (await pasajeros.ConsultarPorCodigoAsync(empresaClienteId, codigoInterno.Trim(), ct) is not null)
            throw new FlowRideExcepcion($"Ya existe un pasajero con el código {codigoInterno.Trim()} en esa empresa.");
    }
}

public sealed class ConductorDominio(IConductorRepositorio conductores)
{
    public async Task<Conductor> ValidarExistaAsync(Guid conductorId, CancellationToken ct) =>
        await conductores.ConsultarPorIdAsync(conductorId, ct)
        ?? throw new FlowRideExcepcion($"El conductor {conductorId} no existe.");

    /// <summary>La licencia (en Colombia, el número de la cédula) identifica a un único conductor.</summary>
    public async Task ValidarLicenciaDisponibleAsync(string numeroLicencia, Guid? conductorId, CancellationToken ct)
    {
        var titular = await conductores.ConsultarPorLicenciaAsync(numeroLicencia.Trim(), ct);
        if (titular is not null && titular.Id != conductorId)
            throw new FlowRideExcepcion("Ya existe un conductor con ese número de licencia.");
    }

    /// <summary>Una franja en hora local: misma zona y el fin después del inicio. Devuelve sus instantes UTC.</summary>
    public static FranjaHoraria ValidarFranja(FechaHoraLocal inicio, FechaHoraLocal fin)
    {
        ArgumentNullException.ThrowIfNull(inicio);
        ArgumentNullException.ThrowIfNull(fin);
        if (inicio.Zona != fin.Zona)
            throw new FlowRideExcepcion("El inicio y el fin van en la misma zona horaria.");
        var (desde, hasta) = (inicio.Instante(), fin.Instante());
        if (hasta <= desde)
            throw new FlowRideExcepcion("La franja debe terminar después de empezar.");
        return new FranjaHoraria(desde, hasta);
    }

    /// <summary>
    /// Disponibilidad programada (RF-07): conductor activo, con licencia vigente el día del servicio (en su zona, no el día UTC), con un turno
    /// vigente que cubre la franja y sin otra asignación vigente que se solape. Sin GPS: no hay cercanía dinámica.
    /// </summary>
    public static IReadOnlyList<Conductor> FiltrarDisponibles(
        IEnumerable<Conductor> candidatos, IReadOnlySet<Guid> ocupados, FranjaHoraria franja, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(franja);
        return candidatos
            .Where(conductor => conductor.EstaActivo
                                && conductor.LicenciaVigenteEn(dia)
                                && conductor.TieneTurnoQueCubre(franja)
                                && !ocupados.Contains(conductor.Id))
            .OrderBy(conductor => conductor.Nombre, StringComparer.Ordinal)
            .ThenBy(conductor => conductor.Id)
            .ToList();
    }

    public static EstadoConductor ValidarEstado(string? estado) =>
        Enum.TryParse<EstadoConductor>(estado?.Trim(), ignoreCase: true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw new FlowRideExcepcion(
                $"El estado '{estado}' no es válido: usa {string.Join(", ", Enum.GetNames<EstadoConductor>())}.");
}

public sealed class VehiculoDominio(IVehiculoRepositorio vehiculos)
{
    public async Task<Vehiculo> ValidarExistaAsync(Guid vehiculoId, CancellationToken ct) =>
        await vehiculos.ConsultarPorIdAsync(vehiculoId, ct)
        ?? throw new FlowRideExcepcion($"El vehículo {vehiculoId} no existe.");

    public async Task ValidarPlacaDisponibleAsync(string placa, CancellationToken ct)
    {
        if (await vehiculos.ConsultarPorPlacaAsync(placa.Trim().ToUpperInvariant(), ct) is not null)
            throw new FlowRideExcepcion($"Ya existe un vehículo con la placa {placa.Trim().ToUpperInvariant()}.");
    }

    /// <summary>Un conductor es habitual de a lo sumo un vehículo.</summary>
    public async Task ValidarConductorSinVehiculoHabitualAsync(Guid conductorId, Guid vehiculoId, CancellationToken ct)
    {
        var actual = await vehiculos.ConsultarPorConductorHabitualAsync(conductorId, ct);
        if (actual is not null && actual.Id != vehiculoId)
            throw new FlowRideExcepcion($"El conductor ya es el conductor habitual del vehículo {actual.Placa}.");
    }

    public static void ValidarCapacidad(int capacidad)
    {
        if (capacidad <= 0)
            throw new FlowRideExcepcion("La capacidad del vehículo debe ser mayor que cero.");
    }
}
