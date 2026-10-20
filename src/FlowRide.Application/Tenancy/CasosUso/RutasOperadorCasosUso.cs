using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Borde;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Tenancy.CasosUso;

/// <summary>
/// Registra o actualiza la ruta del borde de un operador (ADR-0012): su subdominio y su número de WhatsApp no pueden ser
/// de otro operador, para que un subdominio lleve únicamente a su stack (OPLOG-SEG-02). El borde la toma al recargar.
/// </summary>
public sealed class RegistrarRutaOperadorCasoUso(IOperadorRepositorio operadores, IRutaOperadorRepositorio rutas, TimeProvider reloj)
    : ICasoUsoConRetorno<RegistrarRutaOperadorDominio, RutaOperador>
{
    public async Task<RutaOperador> EjecutarAsync(RegistrarRutaOperadorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        _ = await operadores.ConsultarPorIdAsync(datos.OperadorId, ct)
            ?? throw new FlowRideExcepcion($"El operador '{datos.OperadorId}' no existe en el catálogo.");

        var ahora = reloj.GetUtcNow();
        var existente = await rutas.ConsultarPorIdAsync(datos.OperadorId, ct);
        var ruta = existente ?? new RutaOperador(datos.OperadorId, datos.Subdominio, datos.UrlStack, datos.PhoneNumberId, ahora);
        existente?.Actualizar(datos.Subdominio, datos.UrlStack, datos.PhoneNumberId, ahora);

        if (await rutas.ConsultarPorSubdominioAsync(ruta.Subdominio, ct) is { } otra && otra.OperadorId != ruta.OperadorId)
            throw new FlowRideExcepcion($"El subdominio '{ruta.Subdominio}' ya es del operador '{otra.OperadorId}'.");
        if (ruta.PhoneNumberId is { } numero && await rutas.ConsultarPorNumeroAsync(numero, ct) is { } conNumero
            && conNumero.OperadorId != ruta.OperadorId)
            throw new FlowRideExcepcion($"El phone_number_id {numero} ya es del operador '{conNumero.OperadorId}'.");

        if (existente is null)
            await rutas.CrearAsync(ruta, ct);
        else
            await rutas.ModificarAsync(ruta.OperadorId, ruta, ct);
        return ruta;
    }
}

/// <summary>Todas las rutas registradas, en orden por operador.</summary>
public sealed class ConsultarRutasOperadorCasoUso(IRutaOperadorRepositorio rutas)
    : ICasoUsoConRetorno<CargarTablaRutasDominio, IReadOnlyList<RutaOperador>>
{
    public async Task<IReadOnlyList<RutaOperador>> EjecutarAsync(CargarTablaRutasDominio datos, CancellationToken ct) =>
        (await rutas.ConsultarTodosAsync(ct)).OrderBy(r => r.OperadorId, StringComparer.Ordinal).ToList();
}
