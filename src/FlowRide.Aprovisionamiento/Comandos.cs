using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Entrada.Puertos;
using Microsoft.Extensions.DependencyInjection;

namespace FlowRide.Aprovisionamiento;

/// <summary>
/// Adaptador de entrada de la línea de comandos (el equivalente de un controlador): traduce el comando a una petición,
/// llama al puerto de entrada y escribe la respuesta. Solo conoce puertos de entrada y DTO, nunca casos de uso.
/// Devuelve el código de salida del proceso.
/// </summary>
public static class Comandos
{
    public static async Task<int> EjecutarAsync(Comando comando, IServiceProvider servicios, TextWriter salida, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(salida);

        switch (comando.Nombre)
        {
            case Comando.CrearOperador:
                var aprovisionado = await servicios.GetRequiredService<IAprovisionarOperadorPuertoEntrada>().AprovisionarAsync(
                    new AprovisionarOperadorPeticion
                    {
                        TenantId = comando.Opcion("id"),
                        RazonSocial = comando.Opcion("razon-social"),
                        Nit = comando.Opcion("nit"),
                        CorreoContacto = comando.Opcion("correo")
                    }, ct);
                await EscribirAprovisionamientoAsync(aprovisionado, salida);
                return 0;

            case Comando.Migrar:
            case Comando.MigrarTodos:
                var peticion = new MigrarSilosPeticion { TenantId = comando.Nombre == Comando.Migrar ? comando.Opcion("id") : null };
                var migracion = await servicios.GetRequiredService<IMigrarSilosPuertoEntrada>().MigrarAsync(peticion, ct);
                foreach (var silo in migracion.Silos) await EscribirMigracionAsync(silo, salida);
                if (comando.Nombre == Comando.MigrarTodos)
                    await salida.WriteLineAsync($"{migracion.Silos.Count(silo => silo.Exitoso)} de {migracion.Silos.Count} Silos al día.");
                return migracion.TodosExitosos ? 0 : 1;

            case Comando.Listar:
                var operadores = await servicios.GetRequiredService<IConsultarOperadoresPuertoEntrada>().ConsultarAsync(ct);
                if (operadores.Count == 0) await salida.WriteLineAsync("No hay operadores registrados.");
                foreach (var operador in operadores)
                    await salida.WriteLineAsync($"{operador.TenantId,-20} {operador.Estado,-15} {operador.RazonSocial} (NIT {operador.Nit})");
                return 0;

            default:
                throw new InvalidOperationException($"Comando no soportado: '{comando.Nombre}'.");
        }
    }

    private static async Task EscribirAprovisionamientoAsync(AprovisionarOperadorRespuesta respuesta, TextWriter salida)
    {
        await salida.WriteLineAsync(respuesta.OperadorNuevo
            ? $"Operador '{respuesta.TenantId}' registrado en el catálogo."
            : $"Operador '{respuesta.TenantId}' ya existía; datos actualizados.");
        await salida.WriteLineAsync($"  Silo: base y rol '{respuesta.NombreSilo}'.");
        await salida.WriteLineAsync(respuesta.ContrasenaNueva
            ? $"  Secreto: contraseña nueva guardada en el ámbito '{respuesta.AmbitoSecretos}'."
            : $"  Secreto: se reutilizó la contraseña del ámbito '{respuesta.AmbitoSecretos}'.");
        await salida.WriteLineAsync(respuesta.MigracionesAplicadas.Count == 0
            ? "  Esquema: ya estaba al día."
            : $"  Esquema: {respuesta.MigracionesAplicadas.Count} migración(es) aplicada(s): {string.Join(", ", respuesta.MigracionesAplicadas)}.");
        await salida.WriteLineAsync($"  Estado: {respuesta.Estado}. {respuesta.Mensaje}");
    }

    private static Task EscribirMigracionAsync(MigracionSiloRespuesta silo, TextWriter salida)
    {
        if (!silo.Exitoso)
            return salida.WriteLineAsync($"{silo.TenantId}: ERROR {silo.Error}");
        return salida.WriteLineAsync(silo.MigracionesAplicadas.Count == 0
            ? $"{silo.TenantId}: al día."
            : $"{silo.TenantId}: aplicadas {string.Join(", ", silo.MigracionesAplicadas)}.");
    }
}
