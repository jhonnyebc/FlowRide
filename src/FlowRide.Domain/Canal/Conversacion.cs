using System.Globalization;
using System.Text.Json;
using FlowRide.Domain.Entidades;

namespace FlowRide.Domain.Canal;

/// <summary>Paso en que está una conversación del bot (máquina de estados, menú numerado y pasos guiados).</summary>
public enum PasoConversacion
{
    Menu,

    // Pasajero: pedir transporte.
    PedirOrigen,
    PedirDestino,
    PedirFechaHora,
    PedirPasajeros,
    ConfirmarSolicitud,

    // Pasajero: cancelar y calificar.
    ElegirSolicitudCancelar,
    MotivoCancelacion,
    ElegirViajeCalificar,
    PuntajeCalificacion,
    ComentarioCalificacion,

    // Conductor.
    ElegirAsignacion,
    AceptarORechazar,
    MotivoRechazo,
    ElegirViajeIniciar,
    ElegirViajeFinalizar,
    ElegirViajeNovedad,
    TipoNovedad,
    DescripcionNovedad
}

/// <summary>
/// Conversación de un número de WhatsApp con el bot: en qué paso va y los datos que lleva recogidos. Una por teléfono;
/// tras <see cref="Inactividad"/> sin mensajes vuelve al menú.
/// </summary>
public sealed class Conversacion
{
    public static readonly TimeSpan Inactividad = TimeSpan.FromMinutes(30);

    private Dictionary<string, string> _datos = new(StringComparer.Ordinal);

#pragma warning disable CS8618 // Solo para EF Core.
    private Conversacion() { }
#pragma warning restore CS8618

    public Conversacion(Guid id, string telefono, DateTimeOffset ahora)
    {
        Id = Guardas.Id(id, "La conversación requiere un Id.", nameof(id));
        Telefono = Guardas.Texto(telefono, nameof(telefono));
        Paso = PasoConversacion.Menu;
        UltimaActividad = ahora;
    }

    public Guid Id { get; private set; }
    public string Telefono { get; private set; }
    public PasoConversacion Paso { get; private set; }
    public DateTimeOffset UltimaActividad { get; private set; }

    /// <summary>Datos recogidos en el flujo actual, como JSON (jsonb).</summary>
    public string Datos
    {
        get => JsonSerializer.Serialize(_datos);
        private set => _datos = JsonSerializer.Deserialize<Dictionary<string, string>>(value) ?? new(StringComparer.Ordinal);
    }

    /// <summary>¿Pasó demasiado tiempo desde el último mensaje? Entonces el flujo a medias se descarta.</summary>
    public bool Expirada(DateTimeOffset ahora) => ahora - UltimaActividad > Inactividad;

    public void Ir(PasoConversacion paso, DateTimeOffset ahora)
    {
        Paso = paso;
        UltimaActividad = ahora;
    }

    /// <summary>Vuelve al menú y descarta lo recogido.</summary>
    public void Reiniciar(DateTimeOffset ahora)
    {
        _datos = new(StringComparer.Ordinal);
        Ir(PasoConversacion.Menu, ahora);
    }

    public void Guardar(string clave, string valor) => _datos[clave] = valor;

    public string? Leer(string clave) => _datos.GetValueOrDefault(clave);

    public string Requerido(string clave) =>
        Leer(clave) ?? throw new InvalidOperationException($"La conversación no tiene el dato '{clave}'.");

    /// <summary>Guarda las opciones numeradas que se mostraron (Ids), para traducir la respuesta "1", "2"…</summary>
    public void GuardarOpciones(IReadOnlyList<Guid> ids) =>
        Guardar(ClaveOpciones, string.Join(',', ids.Select(id => id.ToString())));

    /// <summary>El Id de la opción elegida, o null si el número no corresponde a ninguna opción mostrada.</summary>
    public Guid? Opcion(int numero)
    {
        var opciones = Leer(ClaveOpciones)?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return numero >= 1 && numero <= opciones.Length ? Guid.Parse(opciones[numero - 1], CultureInfo.InvariantCulture) : null;
    }

    private const string ClaveOpciones = "opciones";
}
