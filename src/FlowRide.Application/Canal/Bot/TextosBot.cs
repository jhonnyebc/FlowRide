using System.Globalization;
using FlowRide.Domain.Enumeraciones;

namespace FlowRide.Application.Canal.Bot;

/// <summary>Textos del bot en español de Colombia. El catálogo configurable de mensajes llega con la Fase 8 (ADR-0016).</summary>
public static class TextosBot
{
    public const string AyudaMenu = "Escribe *menu* en cualquier momento para volver al inicio.";

    public const string NoRegistrado =
        "Este número no está registrado en FlowRide. Pide al operador de transporte que te registre con este número de WhatsApp.";

    public const string Inactivo = "Tu usuario está inactivo en FlowRide. Comunícate con el operador de transporte.";

    public const string SoloTexto = "Por ahora solo entiendo mensajes de texto. " + AyudaMenu;

    public const string OpcionInvalida = "No entendí esa opción. Responde con uno de los números de la lista. " + AyudaMenu;

    public static string MenuPasajero(string nombre) => $"""
        Hola, {PrimerNombre(nombre)}. ¿Qué quieres hacer?
        1. Pedir transporte
        2. Ver mis solicitudes
        3. Cancelar una solicitud
        4. Calificar un viaje
        Responde con el número.
        """;

    public static string MenuConductor(string nombre) => $"""
        Hola, {PrimerNombre(nombre)}. ¿Qué quieres hacer?
        1. Responder asignaciones
        2. Iniciar un viaje
        3. Finalizar un viaje
        4. Reportar una novedad
        Responde con el número.
        """;

    public static string NoSePudo(string motivo) => $"No se pudo completar: {motivo} {AyudaMenu}";

    public static string Lista(string titulo, IEnumerable<string> opciones) =>
        $"{titulo}\n{string.Join('\n', opciones.Select((opcion, i) => $"{i + 1}. {opcion}"))}\nResponde con el número. {AyudaMenu}";

    public static string Fecha(DateTimeOffset local) => local.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);

    public static string Estado(EstadoSolicitud estado) => estado switch
    {
        EstadoSolicitud.Pendiente => "buscando conductor",
        EstadoSolicitud.Asignada => "con conductor asignado",
        EstadoSolicitud.SinCandidatos => "sin conductor disponible por ahora (el operador la está revisando)",
        EstadoSolicitud.EnCurso => "en curso",
        EstadoSolicitud.Completada => "completada",
        _ => "cancelada"
    };

    private static string PrimerNombre(string nombre) => nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? nombre;
}
