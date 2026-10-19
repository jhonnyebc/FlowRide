using System.Text.Json;
using FlowRide.Application.Comun.Salida.Puertos;

namespace FlowRide.Infrastructure.Persistencia.Outbox;

/// <summary>
/// Evento pendiente de publicar (ADR-0007). Es un detalle de infraestructura: lo escribe <see cref="BandejaSalidaEf"/> y lo
/// procesa <c>WorkerNotificaciones</c>: lo toma con <c>FOR UPDATE SKIP LOCKED</c> y lo marca procesado en la misma
/// transacción que guarda sus notificaciones, así un reintento no las duplica (deduplicación por <see cref="EventId"/>).
/// </summary>
public sealed class MensajeOutbox
{
    public const int MaximoIntentos = 10;

    public MensajeOutbox(Guid id, string tipoEvento, string payload, DateTimeOffset ocurridoEn, DateTimeOffset? programadoPara = null)
    {
        Id = id;
        EventId = id;
        TipoEvento = tipoEvento;
        Payload = payload;
        OcurridoEn = ocurridoEn;
        ProximoIntento = programadoPara ?? ocurridoEn;
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

    public void MarcarProcesado(DateTimeOffset ahora) => ProcesadoEn = ahora;

    /// <summary>
    /// Error al procesarlo: se reintenta con espera creciente (2^intentos s, tope 15 min). Al agotar los intentos queda
    /// cerrado con su error, para no bloquear la cola.
    /// </summary>
    public void RegistrarFallo(string error, DateTimeOffset ahora)
    {
        Intentos++;
        UltimoError = error.Length > 2000 ? error[..2000] : error;
        if (Intentos >= MaximoIntentos)
        {
            ProcesadoEn = ahora;
            return;
        }
        ProximoIntento = ahora + TimeSpan.FromSeconds(Math.Min(900, Math.Pow(2, Intentos)));
    }
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

    public Task ProgramarAsync(string tipoEvento, object contenido, DateTimeOffset cuando, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoEvento);
        ArgumentNullException.ThrowIfNull(contenido);
        var ahora = reloj.GetUtcNow();
        contexto.MensajesOutbox.Add(new MensajeOutbox(Guid.CreateVersion7(ahora), tipoEvento, JsonSerializer.Serialize(contenido, Json), ahora, cuando));
        return Task.CompletedTask;
    }
}
