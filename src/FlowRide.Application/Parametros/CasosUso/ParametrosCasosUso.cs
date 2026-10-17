using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Parametros.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Parametros;
using FlowRide.Domain.Parametros.Repositorios;

namespace FlowRide.Application.Parametros.CasosUso;

/// <summary>Todos los parámetros del catálogo con el valor que rige (guardado o por defecto). Lee de la caché.</summary>
public sealed class ConsultarParametrosCasoUso(IParametroRepositorio parametros)
    : ICasoUsoConRetorno<ConsultarParametrosDominio, IReadOnlyList<ParametroVigente>>
{
    public async Task<IReadOnlyList<ParametroVigente>> EjecutarAsync(ConsultarParametrosDominio datos, CancellationToken ct)
    {
        var guardados = (await parametros.ConsultarTodosAsync(ct)).ToDictionary(p => p.Clave, StringComparer.Ordinal);
        return CatalogoParametros.Todas
            .Select(definicion => new ParametroVigente(definicion, guardados.GetValueOrDefault(definicion.Clave)))
            .ToList();
    }
}

/// <summary>
/// Cambia un parámetro (ADR-0016): valida tipo y rango, sube la versión, lo audita y, después de confirmar, invalida
/// la caché local. Las demás réplicas lo ven al vencer su TTL (≤ 60 s, ADR-0006).
/// </summary>
public sealed class CambiarParametroCasoUso(
    IParametroRepositorio parametros,
    ICacheParametros cache,
    IEventoAuditoriaRepositorio auditoria,
    IUnidadTrabajo unidadTrabajo,
    TimeProvider reloj) : ICasoUsoConRetorno<CambiarParametroDominio, ParametroVigente>
{
    public async Task<ParametroVigente> EjecutarAsync(CambiarParametroDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var definicion = CatalogoParametros.Buscar(datos.Clave);
        var ahora = reloj.GetUtcNow();

        var parametro = await parametros.ConsultarPorIdAsync(definicion.Clave, ct);
        var antes = parametro?.Valor ?? definicion.ValorPorDefecto;
        if (parametro is null)
        {
            parametro = new Parametro(definicion, datos.ValorJson, datos.ActorId, ahora);
            await parametros.CrearAsync(parametro, ct);
        }
        else if (parametro.Cambiar(definicion, datos.ValorJson, datos.ActorId, ahora))
        {
            await parametros.ModificarAsync(parametro.Clave, parametro, ct);
        }
        else
        {
            return new ParametroVigente(definicion, parametro);
        }

        await auditoria.CrearAsync(new EventoAuditoria(datos.ActorId,
            AccionesAuditoria.De(AccionesAuditoria.RecursoParametro, AccionesAuditoria.Actualizar),
            AccionesAuditoria.RecursoParametro, parametro.Clave, ahora, ResultadoAuditoria.Exitoso,
            valoresAntes: new Dictionary<string, string?> { ["valor"] = antes },
            valoresDespues: new Dictionary<string, string?>
            {
                ["valor"] = parametro.Valor,
                ["version"] = parametro.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }), ct);
        await unidadTrabajo.GuardarCambiosAsync(ct);
        cache.Invalidar();

        return new ParametroVigente(definicion, parametro);
    }
}
