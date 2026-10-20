using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowRide.Domain.Transversal;

namespace FlowRide.Domain.Parametros;

public enum TipoParametro
{
    Entero,
    Opcion,
    Franjas
}

/// <summary>
/// Definición de un parámetro operativo del operador (ADR-0016): clave estable, tipo, valor por defecto de producto y
/// validación. El valor se guarda como JSON (<c>ops.parametro.valor</c>, jsonb).
/// </summary>
public sealed partial class DefinicionParametro
{
    private DefinicionParametro(string clave, TipoParametro tipo, string descripcion, string valorPorDefecto,
        int minimo = 0, int maximo = 0, IReadOnlyList<string>? opciones = null)
    {
        Clave = clave;
        Tipo = tipo;
        Descripcion = descripcion;
        Minimo = minimo;
        Maximo = maximo;
        Opciones = opciones ?? [];
        ValorPorDefecto = Normalizar(valorPorDefecto);
    }

    public string Clave { get; }
    public TipoParametro Tipo { get; }
    public string Descripcion { get; }
    public string ValorPorDefecto { get; }
    public int Minimo { get; }
    public int Maximo { get; }
    public IReadOnlyList<string> Opciones { get; }

    public static DefinicionParametro Entero(string clave, string descripcion, int porDefecto, int minimo, int maximo) =>
        new(clave, TipoParametro.Entero, descripcion, porDefecto.ToString(CultureInfo.InvariantCulture), minimo, maximo);

    public static DefinicionParametro Opcion(string clave, string descripcion, string porDefecto, params string[] opciones) =>
        new(clave, TipoParametro.Opcion, descripcion, JsonSerializer.Serialize(porDefecto), opciones: opciones);

    public static DefinicionParametro Franjas(string clave, string descripcion) =>
        new(clave, TipoParametro.Franjas, descripcion, "[]");

    /// <summary>
    /// Valida el valor (JSON: número, texto entre comillas o lista de franjas <c>"HH:mm-HH:mm"</c>) y lo devuelve en
    /// forma canónica. Lanza <see cref="FlowRideExcepcion"/> si no cumple el tipo o el rango.
    /// </summary>
    public string Normalizar(string? valorJson)
    {
        JsonElement valor;
        try
        {
            valor = JsonDocument.Parse(valorJson ?? string.Empty).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalido("no es un JSON válido");
        }

        switch (Tipo)
        {
            case TipoParametro.Entero:
                if (valor.ValueKind != JsonValueKind.Number || !valor.TryGetInt32(out var numero))
                    throw Invalido("debe ser un número entero");
                if (numero < Minimo || numero > Maximo)
                    throw Invalido($"debe estar entre {Minimo} y {Maximo}");
                return numero.ToString(CultureInfo.InvariantCulture);

            case TipoParametro.Opcion:
                var opcion = valor.ValueKind == JsonValueKind.String
                    ? Opciones.FirstOrDefault(o => string.Equals(o, valor.GetString(), StringComparison.OrdinalIgnoreCase))
                    : null;
                return opcion is null
                    ? throw Invalido($"debe ser una de: {string.Join(", ", Opciones)}")
                    : JsonSerializer.Serialize(opcion);

            default:
                if (valor.ValueKind != JsonValueKind.Array)
                    throw Invalido("debe ser una lista de franjas \"HH:mm-HH:mm\"");
                var franjas = valor.EnumerateArray()
                    .Select(f => f.ValueKind == JsonValueKind.String ? f.GetString()!.Trim() : string.Empty)
                    .ToList();
                foreach (var franja in franjas)
                {
                    var partes = PatronFranja().Match(franja);
                    if (!partes.Success
                        || !TimeOnly.TryParseExact(partes.Groups[1].Value, "HH:mm", out var inicio)
                        || !TimeOnly.TryParseExact(partes.Groups[2].Value, "HH:mm", out var fin)
                        || fin <= inicio)
                        throw Invalido($"la franja '{franja}' no tiene el formato HH:mm-HH:mm con el fin después del inicio");
                }
                return JsonSerializer.Serialize(franjas.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        }
    }

    private FlowRideExcepcion Invalido(string motivo) => new($"El valor del parámetro '{Clave}' {motivo}.");

    [GeneratedRegex(@"^(\d{2}:\d{2})-(\d{2}:\d{2})$")]
    private static partial Regex PatronFranja();
}

/// <summary>Parámetros de V1 con sus valores por defecto (decididos con el usuario el 2026-10-17; plazos de respuesta y calificación, 2026-10-18).</summary>
public static class CatalogoParametros
{
    public const string AnticipacionRecordatorioMinutos = "recordatorio.anticipacion_minutos";
    public const string VentanaCancelacionMinutos = "solicitud.ventana_cancelacion_minutos";
    public const string FranjasCriticas = "operacion.franjas_criticas";
    public const string VisibilidadSolicitudesPersonales = "solicitud.visibilidad_personales";
    public const string PlazoRespuestaAsignacionMinutos = "asignacion.plazo_respuesta_minutos";
    public const string PlazoCalificacionDias = "calificacion.plazo_dias";
    public const string ToleranciaPuntualidadMinutos = "reporte.tolerancia_puntualidad_minutos";

    public const string VisiblesParaPasajeroYOperadorLogistico = "PasajeroYOperadorLogistico";
    public const string VisiblesSoloParaPasajero = "SoloPasajero";

    public static IReadOnlyList<DefinicionParametro> Todas { get; } =
    [
        DefinicionParametro.Entero(AnticipacionRecordatorioMinutos,
            "Minutos antes del servicio en que se envía el recordatorio al pasajero y al conductor (RF-35).", 60, 5, 1440),
        DefinicionParametro.Entero(VentanaCancelacionMinutos,
            "Minutos antes del servicio hasta los que el pasajero puede modificar o cancelar su solicitud (RF-32).", 30, 0, 1440),
        DefinicionParametro.Franjas(FranjasCriticas,
            "Franjas horarias críticas de la operación, como lista de \"HH:mm-HH:mm\"."),
        DefinicionParametro.Opcion(VisibilidadSolicitudesPersonales,
            "Quién ve las solicitudes personales (PAS-SEG-01).",
            VisiblesParaPasajeroYOperadorLogistico, VisiblesParaPasajeroYOperadorLogistico, VisiblesSoloParaPasajero),
        DefinicionParametro.Entero(PlazoRespuestaAsignacionMinutos,
            "Minutos que tiene el conductor para aceptar o rechazar una asignación; si no responde, vuelve a la cola (RF-34).", 15, 1, 1440),
        DefinicionParametro.Entero(PlazoCalificacionDias,
            "Días después de terminado el viaje en que el pasajero puede calificarlo (RF-54).", 7, 1, 90),
        DefinicionParametro.Entero(ToleranciaPuntualidadMinutos,
            "Minutos después de la hora programada en que un viaje que inicia todavía cuenta como puntual (RF-42, RF-46).", 10, 0, 120)
    ];

    public static DefinicionParametro Buscar(string clave) =>
        Todas.FirstOrDefault(definicion => definicion.Clave == clave?.Trim())
        ?? throw new FlowRideExcepcion($"El parámetro '{clave}' no existe.");
}
