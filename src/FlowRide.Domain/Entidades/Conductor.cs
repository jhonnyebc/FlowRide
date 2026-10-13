using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

public sealed class Conductor
{
    public Conductor(
        Guid id,
        string numeroLicencia,
        DateOnly vencimientoLicencia,
        EstadoConductor estado = EstadoConductor.Activo,
        DateTimeOffset? fechaUltimaAsignacion = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("El conductor requiere un Id.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroLicencia);

        Id = id;
        NumeroLicencia = numeroLicencia;
        VencimientoLicencia = vencimientoLicencia;
        Estado = estado;
        FechaUltimaAsignacion = fechaUltimaAsignacion;
    }

    public Guid Id { get; }
    public string NumeroLicencia { get; }
    public DateOnly VencimientoLicencia { get; }
    public EstadoConductor Estado { get; }
    public DateTimeOffset? FechaUltimaAsignacion { get; }

    public bool EstaActivo => Estado == EstadoConductor.Activo;

    public bool LicenciaVigenteEn(DateOnly fecha) => VencimientoLicencia >= fecha;
}
