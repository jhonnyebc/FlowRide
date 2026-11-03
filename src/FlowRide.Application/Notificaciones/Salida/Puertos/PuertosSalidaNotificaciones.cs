using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Notificaciones;

namespace FlowRide.Application.Notificaciones.Salida.Puertos;

/// <summary>Envío de correo (ADR-0015, <c>IEmailSender</c>): SMTP genérico, sin fijar proveedor. Mailpit en local.</summary>
public interface IEnviadorCorreo
{
    /// <summary>Envía el correo; lanza si el servidor no lo acepta (el caso de uso lo reintenta).</summary>
    Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct);
}

/// <summary>Lado de lectura de las alertas del panel (lista, y las nuevas para el envío en vivo por SSE).</summary>
public interface IConsultasAlertas
{
    Task<PaginaResultado<AlertaLectura>> BuscarAsync(bool soloNoLeidas, Paginacion paginacion, CancellationToken ct);

    /// <summary>Las alertas con secuencia mayor que la dada, en orden.</summary>
    Task<IReadOnlyList<AlertaLectura>> PosterioresAsync(long despuesDe, int maximo, CancellationToken ct);
}

public sealed record AlertaLectura(
    Guid Id,
    long Secuencia,
    string Tipo,
    SeveridadAlerta Severidad,
    string Titulo,
    string Detalle,
    DateTimeOffset CreadaEn,
    Guid? ReferenciaId,
    DateTimeOffset? LeidaEn);
