using FlowRide.Domain.Enumeraciones;
using FlowRide.Domain.ValoresObjeto;

namespace FlowRide.Domain.Entidades;

/// <summary>
/// Conductor del operador; interactúa solo por WhatsApp. Su disponibilidad no se persiste como estado:
/// se deriva de sus turnos y asignaciones vigentes, lo que hace reproducible cada decisión del motor.
/// </summary>
public sealed class Conductor : Usuario
{
    private readonly List<Turno> _turnos = [];
    private readonly List<DocumentoConductor> _documentos = [];

    public Conductor(
        Guid id,
        string nombre,
        string telefonoWhatsApp,
        Guid rolId,
        DateTimeOffset creadoEn,
        string numeroLicencia,
        string categoriaLicencia,
        DateOnly vencimientoLicencia,
        string? correo = null,
        EstadoConductor estado = EstadoConductor.Activo,
        DateTimeOffset? fechaUltimaAsignacion = null)
        : base(id, nombre, telefonoWhatsApp, rolId, creadoEn, correo)
    {
        NumeroLicencia = Guardas.Texto(numeroLicencia, nameof(numeroLicencia));
        CategoriaLicencia = Guardas.Texto(categoriaLicencia, nameof(categoriaLicencia));
        VencimientoLicencia = vencimientoLicencia;
        Estado = estado;
        FechaUltimaAsignacion = fechaUltimaAsignacion;
    }

    public string NumeroLicencia { get; private set; }
    public string CategoriaLicencia { get; private set; }
    public DateOnly VencimientoLicencia { get; private set; }
    public EstadoConductor Estado { get; private set; }
    public DateTimeOffset? FechaUltimaAsignacion { get; private set; }

    public IReadOnlyList<Turno> Turnos => _turnos;
    public IReadOnlyList<DocumentoConductor> Documentos => _documentos;

    /// <summary>Puede recibir asignaciones: usuario con acceso activo y estado operativo Activo.</summary>
    public bool EstaActivo => Activo && Estado == EstadoConductor.Activo;

    public bool LicenciaVigenteEn(DateOnly fecha) => VencimientoLicencia >= fecha;

    public bool TieneTurnoQueCubre(FranjaHoraria franja) => _turnos.Any(turno => turno.Cubre(franja));

    /// <summary>Programa un turno; dos turnos vigentes del mismo conductor no pueden solaparse.</summary>
    public Turno ProgramarTurno(Guid turnoId, FranjaHoraria franja)
    {
        ArgumentNullException.ThrowIfNull(franja);
        if (_turnos.Any(turno => turno.EstaVigente && turno.Franja.SeSolapaCon(franja)))
            throw new InvalidOperationException("El turno se solapa con otro turno vigente del conductor.");

        var turno = new Turno(turnoId, Id, franja.Inicio, franja.Fin);
        _turnos.Add(turno);
        return turno;
    }

    public void AbandonarTurno(Turno turno, string motivo)
    {
        ArgumentNullException.ThrowIfNull(turno);
        if (turno.ConductorId != Id)
            throw new InvalidOperationException("El turno no pertenece a este conductor.");

        turno.MarcarAbandonado(motivo);
    }

    public DocumentoConductor AgregarDocumento(Guid documentoId, TipoDocumento tipo, string referenciaBlob, DateOnly? fechaVencimiento)
    {
        var documento = new DocumentoConductor(documentoId, Id, tipo, referenciaBlob, fechaVencimiento);
        _documentos.Add(documento);
        return documento;
    }

    /// <summary>Quita el documento del conductor; el archivo en el almacenamiento lo borra el caso de uso.</summary>
    public DocumentoConductor QuitarDocumento(Guid documentoId)
    {
        var documento = _documentos.SingleOrDefault(d => d.Id == documentoId)
                        ?? throw new InvalidOperationException("El documento no pertenece a este conductor.");
        _documentos.Remove(documento);
        return documento;
    }

    public void AceptarAsignacion(Asignacion asignacion)
    {
        ValidarPropia(asignacion);
        asignacion.Aceptar();
    }

    public void RechazarAsignacion(Asignacion asignacion, string motivo)
    {
        ValidarPropia(asignacion);
        asignacion.Rechazar(motivo);
    }

    /// <summary>Alimenta el desempate "mayor tiempo desde la última asignación" (ADR-0009).</summary>
    public void RegistrarAsignacion(DateTimeOffset asignadaEn)
    {
        if (FechaUltimaAsignacion is null || asignadaEn > FechaUltimaAsignacion)
            FechaUltimaAsignacion = asignadaEn;
    }

    public void ActualizarLicencia(string numeroLicencia, string categoriaLicencia, DateOnly vencimientoLicencia)
    {
        NumeroLicencia = Guardas.Texto(numeroLicencia, nameof(numeroLicencia));
        CategoriaLicencia = Guardas.Texto(categoriaLicencia, nameof(categoriaLicencia));
        VencimientoLicencia = vencimientoLicencia;
    }

    /// <summary>Activación e inactivación operativa del conductor (RF-28).</summary>
    public void CambiarEstado(EstadoConductor estado) => Estado = estado;

    private void ValidarPropia(Asignacion asignacion)
    {
        ArgumentNullException.ThrowIfNull(asignacion);
        if (asignacion.ConductorId != Id)
            throw new InvalidOperationException("La asignación no pertenece a este conductor.");
    }
}
