using FlowRide.Application.Canal.Salida.Puertos;
using FlowRide.Domain.Canal;
using FlowRide.Domain.Canal.Repositorios;
using FlowRide.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace FlowRide.Infrastructure.Canal;

public sealed class MensajeEntranteRepositorioAdaptador(FlowRideDbContext contexto) : IMensajeEntranteRepositorio
{
    public Task CrearAsync(MensajeEntrante entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.MensajesEntrantes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<MensajeEntrante?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.MensajesEntrantes.SingleOrDefaultAsync(m => m.Id == id, ct);

    public Task<bool> ExisteAsync(string idExterno, CancellationToken ct) =>
        contexto.MensajesEntrantes.AnyAsync(m => m.IdExterno == idExterno, ct);

    public async Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(int maximo, CancellationToken ct) =>
        await contexto.MensajesEntrantes.AsNoTracking()
            .Where(m => m.Estado == EstadoMensajeEntrante.Pendiente)
            .OrderBy(m => m.RecibidoEn).ThenBy(m => m.Id)
            .Select(m => m.Id).Take(maximo)
            .ToListAsync(ct);
}

public sealed class MensajeSalienteRepositorioAdaptador(FlowRideDbContext contexto) : IMensajeSalienteRepositorio
{
    public Task CrearAsync(MensajeSaliente entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.MensajesSalientes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<MensajeSaliente?> ConsultarPorIdAsync(Guid id, CancellationToken ct) =>
        contexto.MensajesSalientes.SingleOrDefaultAsync(m => m.Id == id, ct);

    public Task<MensajeSaliente?> ConsultarPorIdExternoAsync(string idExterno, CancellationToken ct) =>
        contexto.MensajesSalientes.FirstOrDefaultAsync(m => m.IdExterno == idExterno, ct);

    public async Task<IReadOnlyList<Guid>> ConsultarPendientesAsync(DateTimeOffset ahora, int maximo, CancellationToken ct) =>
        await contexto.MensajesSalientes.AsNoTracking()
            .Where(m => m.Estado == EstadoMensajeSaliente.Pendiente && m.ProximoIntento <= ahora)
            .OrderBy(m => m.CreadoEn).ThenBy(m => m.Id)
            .Select(m => m.Id).Take(maximo)
            .ToListAsync(ct);
}

public sealed class ConversacionRepositorioAdaptador(FlowRideDbContext contexto) : IConversacionRepositorio
{
    public Task CrearAsync(Conversacion entidad, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entidad);
        contexto.Conversaciones.Add(entidad);
        return Task.CompletedTask;
    }

    public async Task<Conversacion?> ConsultarPorTelefonoAsync(string telefono, CancellationToken ct) =>
        contexto.Conversaciones.Local.FirstOrDefault(c => c.Telefono == telefono)
        ?? await contexto.Conversaciones.SingleOrDefaultAsync(c => c.Telefono == telefono, ct);
}

/// <summary>Lado de lectura del canal: entrantes y salientes de un número, en orden (el teléfono se compara cifrado).</summary>
public sealed class ConsultasCanalEf(FlowRideDbContext contexto) : IConsultasCanal
{
    public async Task<IReadOnlyList<MensajeCanalLectura>> ConversacionAsync(string telefono, int maximo, CancellationToken ct)
    {
        var entrantes = await contexto.MensajesEntrantes.AsNoTracking().Where(m => m.Remitente == telefono)
            .OrderByDescending(m => m.RecibidoEn).Take(maximo)
            .Select(m => new MensajeCanalLectura(m.Id, "entrante", m.Contenido, m.RecibidoEn, m.Estado.ToString(), null, m.UltimoError))
            .ToListAsync(ct);
        var salientes = await contexto.MensajesSalientes.AsNoTracking().Where(m => m.Destinatario == telefono)
            .OrderByDescending(m => m.CreadoEn).Take(maximo)
            .Select(m => new MensajeCanalLectura(m.Id, "saliente", m.Contenido, m.CreadoEn, m.Estado.ToString(), m.EstadoExterno, m.UltimoError))
            .ToListAsync(ct);

        return entrantes.Concat(salientes).OrderByDescending(m => m.FechaHora).ThenByDescending(m => m.Id).Take(maximo)
            .OrderBy(m => m.FechaHora).ThenBy(m => m.Id).ToList();
    }
}
