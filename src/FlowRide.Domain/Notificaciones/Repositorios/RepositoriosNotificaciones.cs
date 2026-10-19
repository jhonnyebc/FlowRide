using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Notificaciones.Repositorios;

// Notificaciones del Silo (esquema notif). Crear y modificar dejan el cambio pendiente: lo confirma la unidad de trabajo.

public interface IAlertaRepositorio : ICrearRepositorio<Alerta>
{
    /// <summary>La alerta rastreada, para marcarla como leída.</summary>
    Task<Alerta?> ConsultarPorIdAsync(Guid id, CancellationToken ct);
}

public interface ICorreoSalienteRepositorio : ICrearRepositorio<CorreoSaliente>
{
    Task<CorreoSaliente?> ConsultarPorIdAsync(Guid id, CancellationToken ct);

    /// <summary>Pendientes cuyo próximo intento ya llegó, los más viejos primero.</summary>
    Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct);
}

public interface IPlantillaWhatsAppRepositorio
{
    /// <summary>La plantilla de la clave (rastreada), o null si la clave no tiene registro.</summary>
    Task<PlantillaWhatsApp?> ConsultarAsync(string clave, CancellationToken ct);

    Task<IReadOnlyList<PlantillaWhatsApp>> ConsultarTodasAsync(CancellationToken ct);
}
