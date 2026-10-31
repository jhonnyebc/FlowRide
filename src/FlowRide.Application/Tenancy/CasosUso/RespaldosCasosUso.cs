using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Plataforma.Respaldos;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Tenancy.CasosUso;

/// <summary>
/// Guarda en el bucket del operador un respaldo de su Silo (ADR-0013) y aplica la retención de 35 días: borra los
/// vencidos, pero nunca el más reciente. El respaldo nuevo queda guardado antes de borrar nada.
/// </summary>
public sealed class GuardarRespaldoCasoUso(IOperadorRepositorio operadores, IAlmacenRespaldos almacen, TimeProvider reloj)
    : ICasoUsoConRetorno<GuardarRespaldoDominio, ResultadoRespaldo>
{
    public async Task<ResultadoRespaldo> EjecutarAsync(GuardarRespaldoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var bucket = await BucketDelOperadorAsync(operadores, datos.OperadorId, ct);

        var nombre = PoliticaRetencionRespaldos.NombrePara(reloj.GetUtcNow());
        await almacen.GuardarAsync(bucket, nombre, datos.Contenido, ct);

        var respaldos = await almacen.ListarAsync(bucket, ct);
        var guardado = respaldos.FirstOrDefault(respaldo => respaldo.Nombre == nombre)
                       ?? throw new FlowRideExcepcion($"El respaldo '{nombre}' no quedó en el bucket del operador.");
        var vencidos = PoliticaRetencionRespaldos.Vencidos(respaldos, reloj.GetUtcNow());
        foreach (var vencido in vencidos)
            await almacen.EliminarAsync(bucket, vencido.Nombre, ct);

        return new ResultadoRespaldo(guardado, vencidos);
    }

    internal static async Task<string> BucketDelOperadorAsync(IOperadorRepositorio operadores, string operadorId, CancellationToken ct) =>
        (await operadores.ConsultarPorIdAsync(operadorId, ct)
         ?? throw new FlowRideExcepcion($"El operador '{operadorId}' no existe en el catálogo.")).NombreBucket;
}

/// <summary>Los respaldos de un operador, del más reciente al más antiguo.</summary>
public sealed class ConsultarRespaldosCasoUso(IOperadorRepositorio operadores, IAlmacenRespaldos almacen)
    : ICasoUsoConRetorno<ConsultarRespaldosDominio, IReadOnlyList<Respaldo>>
{
    public async Task<IReadOnlyList<Respaldo>> EjecutarAsync(ConsultarRespaldosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        return await almacen.ListarAsync(await GuardarRespaldoCasoUso.BucketDelOperadorAsync(operadores, datos.OperadorId, ct), ct);
    }
}

/// <summary>Descarga un respaldo del operador (el indicado o el más reciente) para restaurarlo.</summary>
public sealed class DescargarRespaldoCasoUso(IOperadorRepositorio operadores, IAlmacenRespaldos almacen)
    : ICasoUsoConRetorno<DescargarRespaldoDominio, Respaldo>
{
    public async Task<Respaldo> EjecutarAsync(DescargarRespaldoDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var bucket = await GuardarRespaldoCasoUso.BucketDelOperadorAsync(operadores, datos.OperadorId, ct);
        var respaldos = await almacen.ListarAsync(bucket, ct);
        var respaldo = (datos.Nombre is null
                           ? respaldos.FirstOrDefault()
                           : respaldos.FirstOrDefault(r => r.Nombre == datos.Nombre))
                       ?? throw new FlowRideExcepcion(datos.Nombre is null
                           ? $"El operador '{datos.OperadorId}' no tiene respaldos."
                           : $"El operador '{datos.OperadorId}' no tiene el respaldo '{datos.Nombre}'.");

        await almacen.DescargarAsync(bucket, respaldo.Nombre, datos.Destino, ct);
        return respaldo;
    }
}
