using System.Text.Json;
using FlowRide.Application.Comun.Salida.Puertos;

namespace FlowRide.Infrastructure.Persistencia.Outbox;

/// <summary>
/// Evento pendiente de publicar (ADR-0007). Es un detalle de infraestructura: lo escribe <see cref="BandejaSalidaEf"/> y lo
/// procesa el worker de la Fase 8 (marcado atómico, reintentos y deduplicación por <see cref="EventId"/>).
/// </summary>
public sealed class MensajeOutbox
{
    public MensajeOutbox(Guid id, string tipoEvento, string payload, DateTimeOffset ocurridoEn)
    {
        Id = id;
        EventId = id;
        TipoEvento = tipoEvento;
        Payload = payload;
        OcurridoEn = ocurridoEn;
        ProximoIntento = ocurridoEn;
    }

#pragma warning disable CS8618 // Solo para EF Core.
    private MensajeOutbox()
    {
    }
#pragma warning restore CS8618

    public Guid Id { get; private set; }

    /// <summary>Identificador de deduplicación para los consumidores.</summary>
    public Guid EventId { get; private set; }

    public string TipoEvento { get; private set; }
    public string Payload { get; private set; }
    public DateTimeOffset OcurridoEn { get; private set; }
    public DateTimeOffset ProximoIntento { get; private set; }
    public short Intentos { get; private set; }
    public DateTimeOffset? ProcesadoEn { get; private set; }
    public string? UltimoError { get; private set; }
}

/// <summary>Escribe los eventos en el mismo contexto del Silo: se confirman junto con el cambio que los produce.</summary>
public sealed class BandejaSalidaEf(FlowRideDbContext contexto, TimeProvider reloj) : IBandejaSalida
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task AgregarAsync(string tipoEvento, object contenido, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoEvento);
        ArgumentNullException.ThrowIfNull(contenido);
        var ahora = reloj.GetUtcNow();
        contexto.MensajesOutbox.Add(new MensajeOutbox(Guid.CreateVersion7(ahora), tipoEvento, JsonSerializer.Serialize(contenido, Json), ahora));
        return Task.CompletedTask;
    }
}
