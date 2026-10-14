using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

/// <summary>Soporte documental del conductor; el archivo vive en el almacenamiento de objetos (ADR-0013).</summary>
public sealed class DocumentoConductor
{
    public DocumentoConductor(Guid id, Guid conductorId, TipoDocumento tipo, string referenciaBlob, DateOnly? fechaVencimiento)
    {
        Id = Guardas.Id(id, "El documento requiere un Id.", nameof(id));
        ConductorId = Guardas.Id(conductorId, "El documento requiere un conductor.", nameof(conductorId));
        Tipo = tipo;
        ReferenciaBlob = Guardas.Texto(referenciaBlob, nameof(referenciaBlob));
        FechaVencimiento = fechaVencimiento;
    }

    public Guid Id { get; }
    public Guid ConductorId { get; }
    public TipoDocumento Tipo { get; }

    /// <summary>Ruta del archivo dentro del bucket del operador (prefijo <c>documents/</c>).</summary>
    public string ReferenciaBlob { get; }

    public DateOnly? FechaVencimiento { get; }

    public bool VigenteEn(DateOnly fecha) => FechaVencimiento is null || FechaVencimiento >= fecha;
}
