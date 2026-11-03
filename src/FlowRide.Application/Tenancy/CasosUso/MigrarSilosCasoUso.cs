using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.MigrarSilos;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Tenancy.CasosUso;

public sealed record ResultadoMigracion(string TenantId, IReadOnlyList<string> MigracionesAplicadas, string? Error)
{
    public bool Exitoso => Error is null;
}

/// <summary>
/// Aplica las migraciones pendientes a los Silos ya aprovisionados (despliegue por oleadas, ADR-0010).
/// Cada Silo registra su propia versión de esquema; las migraciones son reintentables (ADR-0002).
/// Con un operador concreto un fallo se propaga; con "todos", un Silo que falla no detiene a los demás.
/// </summary>
public sealed class MigrarSilosCasoUso(
    OperadorDominio operadorDominio,
    IOperadorRepositorio operadores,
    IMigradorSilo migrador,
    IAlmacenSecretos secretos) : ICasoUsoConRetorno<MigrarSilosDominio, IReadOnlyList<ResultadoMigracion>>
{
    public async Task<IReadOnlyList<ResultadoMigracion>> EjecutarAsync(MigrarSilosDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (!datos.EsTodos)
        {
            var operador = await operadorDominio.ValidarOperadorExistaAsync(datos.TenantId!, ct);
            return [await MigrarAsync(operador, ct)];
        }

        var resultados = new List<ResultadoMigracion>();
        var vigentes = (await operadores.ConsultarTodosAsync(ct))
            .Where(operador => operador.Estado != EstadoOperador.Suspendido)
            .OrderBy(operador => operador.Id, StringComparer.Ordinal);

        foreach (var operador in vigentes)
        {
            try
            {
                resultados.Add(await MigrarAsync(operador, ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultados.Add(new ResultadoMigracion(operador.Id, [], ex.Message));
            }
        }

        return resultados;
    }

    private async Task<ResultadoMigracion> MigrarAsync(Operador operador, CancellationToken ct)
    {
        var cadenaConexion = await secretos.ObtenerAsync(operador.AmbitoSecretos, ClavesSecretos.CadenaConexionSilo, ct)
            ?? throw new FlowRideExcepcion(
                $"Falta el secreto '{ClavesSecretos.CadenaConexionSilo}' del operador '{operador.Id}'. " +
                "Vuelve a aprovisionarlo con 'crear-operador'.");

        var aplicadas = await migrador.MigrarAsync(cadenaConexion, ct);
        return new ResultadoMigracion(operador.Id, aplicadas, null);
    }
}
