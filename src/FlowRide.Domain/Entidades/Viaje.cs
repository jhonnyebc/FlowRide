using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Domain.Entidades;

/// <summary>Ejecución de una asignación. Ciclo: Programado → EnCurso → Finalizado, o Cancelado.</summary>
public sealed class Viaje
{
    private readonly List<Parada> _paradas = [];
    private readonly List<Incidente> _incidentes = [];

    public Viaje(Guid id, Guid asignacionId)
    {
        Id = Guardas.Id(id, "El viaje requiere un Id.", nameof(id));
        AsignacionId = Guardas.Id(asignacionId, "El viaje requiere una asignación.", nameof(asignacionId));
        Estado = EstadoViaje.Programado;
    }

    public Guid Id { get; }
    public Guid AsignacionId { get; }
    public EstadoViaje Estado { get; private set; }
    public DateTimeOffset? FechaHoraInicio { get; private set; }
    public DateTimeOffset? FechaHoraFin { get; private set; }
    public IReadOnlyList<Parada> Paradas => _paradas;
    public IReadOnlyList<Incidente> Incidentes => _incidentes;

    public void Iniciar(DateTimeOffset ahora)
    {
        if (Estado != EstadoViaje.Programado)
            throw Guardas.TransicionInvalida("iniciar", "el viaje", Estado);
        FechaHoraInicio = ahora;
        Estado = EstadoViaje.EnCurso;
    }

    public void Finalizar(DateTimeOffset ahora)
    {
        if (Estado != EstadoViaje.EnCurso)
            throw Guardas.TransicionInvalida("finalizar", "el viaje", Estado);
        if (ahora < FechaHoraInicio)
            throw new ArgumentException("El fin del viaje no puede ser anterior a su inicio.", nameof(ahora));
        FechaHoraFin = ahora;
        Estado = EstadoViaje.Finalizado;
    }

    public void Cancelar()
    {
        if (Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso))
            throw Guardas.TransicionInvalida("cancelar", "el viaje", Estado);
        Estado = EstadoViaje.Cancelado;
    }

    public Parada RegistrarParada(Guid paradaId, string direccion, int orden, DateTimeOffset horaEstimada)
    {
        ExigirAbierto("registrar paradas en");
        if (_paradas.Any(parada => parada.Orden == orden))
            throw new InvalidOperationException($"Ya existe una parada con orden {orden} en este viaje.");

        var parada = new Parada(paradaId, Id, orden, direccion, horaEstimada);
        _paradas.Add(parada);
        return parada;
    }

    public Incidente RegistrarIncidente(Guid incidenteId, TipoIncidente tipo, string descripcion, Guid reportadoPorId, DateTimeOffset ahora)
    {
        ExigirAbierto("registrar incidentes en");
        var incidente = new Incidente(incidenteId, Id, tipo, descripcion, ahora, reportadoPorId);
        _incidentes.Add(incidente);
        return incidente;
    }

    private void ExigirAbierto(string accion)
    {
        if (Estado is not (EstadoViaje.Programado or EstadoViaje.EnCurso))
            throw Guardas.TransicionInvalida(accion, "el viaje", Estado);
    }
}
