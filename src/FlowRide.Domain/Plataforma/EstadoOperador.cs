namespace FlowRide.Domain.Plataforma;

public enum EstadoOperador
{
    /// <summary>Registrado en el catálogo; su Silo aún no termina de aprovisionarse.</summary>
    Aprovisionando,
    Activo,
    Suspendido
}
