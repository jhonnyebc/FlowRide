using FlowRide.Application.Seguridad.Salida.Puertos;
using Npgsql;

namespace FlowRide.Infrastructure.Seguridad;

/// <summary>
/// Reescribe con la clave vigente los campos cifrados del Silo que estén en claro (datos anteriores a la Fase 3) o con
/// una versión anterior de la clave. Trabaja con SQL directo, fila por fila, en una sola transacción.
/// </summary>
public sealed class RecifradorCamposSilo(NpgsqlDataSource fuente, CifradorCampos cifrador) : IRecifradorCampos
{
    /// <summary>Columnas cifradas del Silo: deben coincidir con las conversiones de <c>FlowRideDbContext</c>.</summary>
    internal static readonly (string Tabla, string Columna)[] Campos =
    [
        ("ops.usuario", "telefono_whats_app"),
        ("ops.conductor", "numero_licencia")
    ];

    public async Task<int> RecifrarAsync(CancellationToken ct)
    {
        await using var conexion = await fuente.OpenConnectionAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        var reescritos = 0;
        foreach (var (tabla, columna) in Campos)
        {
            var pendientes = new List<(Guid Id, string Valor)>();
            await using (var lectura = new NpgsqlCommand($"SELECT id, {columna} FROM {tabla} FOR UPDATE", conexion, transaccion))
            await using (var filas = await lectura.ExecuteReaderAsync(ct))
            {
                while (await filas.ReadAsync(ct))
                {
                    var valor = filas.GetString(1);
                    if (!cifrador.EstaCifradoConVigente(valor))
                        pendientes.Add((filas.GetGuid(0), valor));
                }
            }

            foreach (var (id, valor) in pendientes)
            {
                await using var escritura = new NpgsqlCommand($"UPDATE {tabla} SET {columna} = @valor WHERE id = @id", conexion, transaccion);
                escritura.Parameters.AddWithValue("valor", cifrador.Cifrar(cifrador.Descifrar(valor)));
                escritura.Parameters.AddWithValue("id", id);
                await escritura.ExecuteNonQueryAsync(ct);
            }

            reescritos += pendientes.Count;
        }

        await transaccion.CommitAsync(ct);
        return reescritos;
    }
}
