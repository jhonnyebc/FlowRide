using System.Net;
using System.Net.Mail;
using FlowRide.Application.Notificaciones.Salida.Puertos;
using FlowRide.Domain.Comun.Consultas;
using FlowRide.Domain.Notificaciones;
using FlowRide.Domain.Notificaciones.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Notificaciones;

public sealed class AlertaRepositorioAdaptador(FlowRideDbContext contexto) : IAlertaRepositorio
{
    public Task CrearAsync(Alerta entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Alertas.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<Alerta?> ConsultarPorIdAsync(Guid id, CancellationToken ct) => contexto.Alertas.SingleOrDefaultAsync(a => a.Id == id, ct);
}

public sealed class CorreoSalienteRepositorioAdaptador(FlowRideDbContext contexto) : ICorreoSalienteRepositorio
{
    public Task CrearAsync(CorreoSaliente entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.CorreosSalientes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<CorreoSaliente?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.CorreosSalientes.SingleOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct) =>
        await contexto.CorreosSalientes.AsNoTracking()
            .Where(c => c.Estado == EstadoCorreo.Pendiente && c.ProximoIntento <= ahora)
            .OrderBy(c => c.CreadoEn).ThenBy(c => c.Id)
            .Select(c => c.Id).Take(maximo)
            .ToListAsync(ct);
}

public sealed class PlantillaWhatsAppRepositorioAdaptador(FlowRideDbContext contexto) : IPlantillaWhatsAppRepositorio
{
    public Task<PlantillaWhatsApp?> ConsultarAsync(string clave, CancellationToken ct) =>
        contexto.PlantillasWhatsApp.SingleOrDefaultAsync(p => p.Clave == clave, ct);

    public async Task<IReadOnlyList<PlantillaWhatsApp>> ConsultarTodasAsync(CancellationToken ct) =>
        await contexto.PlantillasWhatsApp.AsNoTracking().OrderBy(p => p.Clave).ToListAsync(ct);
}

/// <summary>Lado de lectura de las alertas: lista paginada y las posteriores a una secuencia (envío en vivo).</summary>
public sealed class ConsultasAlertasEf(FlowRideDbContext contexto) : IConsultasAlertas
{
    public async Task<PaginaResultado<AlertaLectura>> BuscarAsync(bool soloNoLeidas, Paginacion paginacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paginacion);
        var consulta = contexto.Alertas.AsNoTracking();
        if (soloNoLeidas)
            consulta = consulta.Where(a => a.LeidaEn == null);

        var total = await consulta.CountAsync(ct);
        var elementos = await Lectura(consulta.OrderByDescending(a => a.Secuencia).Skip(paginacion.Salto).Take(paginacion.Tamano)).ToListAsync(ct);
        return new PaginaResultado<AlertaLectura>(elementos, total, paginacion);
    }

    public async Task<IReadOnlyList<AlertaLectura>> PosterioresAsync(long despuesDe, int maximo, CancellationToken ct) =>
        await Lectura(contexto.Alertas.AsNoTracking().Where(a => a.Secuencia > despuesDe).OrderBy(a => a.Secuencia).Take(maximo)).ToListAsync(ct);

    private static IQueryable<AlertaLectura> Lectura(IQueryable<Alerta> alertas) =>
        alertas.Select(a => new AlertaLectura(a.Id, a.Secuencia, a.Tipo, a.Severidad, a.Titulo, a.Detalle, a.CreadaEn, a.ReferenciaId, a.LeidaEn));
}

/// <summary>Configuración del correo saliente del stack (no secreta; usuario y contraseña, si hay, son secretos).</summary>
public sealed class CorreoOptions
{
    public const string Seccion = "FlowRide:Correo";

    /// <summary>Servidor SMTP; en local, Mailpit (puerto 1025).</summary>
    public string Host { get; set; } = "localhost";

    public int Puerto { get; set; } = 1025;
    public bool UsarSsl { get; set; }
    public string Remitente { get; set; } = "no-responder@flowride.local";
    public string? Usuario { get; set; }
    public string? Contrasena { get; set; }
}

/// <summary><c>IEmailSender</c> por SMTP (ADR-0015), sin fijar proveedor: Mailpit en local, el del operador en producción.</summary>
public sealed class EnviadorCorreoSmtp(CorreoOptions opciones) : IEnviadorCorreo
{
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpo, CancellationToken ct)
    {
        using var mensaje = new MailMessage(opciones.Remitente, destinatario, asunto, cuerpo);
        using var cliente = new SmtpClient(opciones.Host, opciones.Puerto) { EnableSsl = opciones.UsarSsl };
        if (!string.IsNullOrEmpty(opciones.Usuario))
            cliente.Credentials = new NetworkCredential(opciones.Usuario, opciones.Contrasena);
        await cliente.SendMailAsync(mensaje, ct);
    }
}
