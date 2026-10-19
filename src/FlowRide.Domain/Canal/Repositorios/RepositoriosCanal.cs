using FlowRide.Domain.Comun.Repositorios;

namespace FlowRide.Domain.Canal.Repositorios;

// Canal WhatsApp del Silo (esquema canal). Crear y modificar dejan el cambio pendiente: lo confirma la unidad de trabajo.

public interface IMensajeEntranteRepositorio : ICrearRepositorio<MensajeEntrante>
{
    /// <summary>El mensaje rastreado, para procesarlo.</summary>
    Task<MensajeEntrante?> ConsultarPorIdAsync(Guid id, CancellationToken ct);

    /// <summary>¿Ya se recibió un mensaje con ese identificador del proveedor? (idempotencia del webhook)</summary>
    Task<bool> ExisteAsync(string idExterno, CancellationToken ct);

    /// <summary>Pendientes en el orden en que llegaron.</summary>
    Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(int maximo, CancellationToken ct);

    /// <summary>Cuándo escribió por última vez el teléfono (ventana de 24 h de WhatsApp), o null si nunca.</summary>
    Task<DateTimeOffset?> UltimoRecibidoAsync(string telefono, CancellationToken ct);
}

public interface IMensajeSalienteRepositorio : ICrearRepositorio<MensajeSaliente>
{
    Task<MensajeSaliente?> ConsultarPorIdAsync(Guid id, CancellationToken ct);

    Task<MensajeSaliente?> ConsultarPorIdExternoAsync(string idExterno, CancellationToken ct);

    /// <summary>Pendientes cuyo próximo intento ya llegó, los más viejos primero.</summary>
    Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct);
}

public interface IConversacionRepositorio : ICrearRepositorio<Conversacion>
{
    /// <summary>La conversación del teléfono (rastreada), o null si nunca escribió.</summary>
    Task<Conversacion?> ConsultarPorTelefonoAsync(string telefono, CancellationToken ct);
}
