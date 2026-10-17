using FlowRide.Application.Comun.CasosUso;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.AprovisionarOperador;
using FlowRide.Domain.Plataforma.Repositorios;

namespace FlowRide.Application.Tenancy.CasosUso;

public sealed record ResultadoAprovisionamiento(
    Operador Operador,
    bool OperadorNuevo,
    bool ContrasenaNueva,
    IReadOnlyList<string> MigracionesAplicadas);

/// <summary>
/// Registro de un operador como tenant (RF-03). Pipeline idempotente de ADR-0010: catálogo → secreto → base y rol
/// → migraciones → operador activo. Si falla a mitad, se puede volver a ejecutar con los mismos datos.
/// El realm de Keycloak (Fase 2) y la ruta en el borde (Fase 10) se agregan a este pipeline en sus fases.
/// </summary>
public sealed class AprovisionarOperadorCasoUso(
    OperadorDominio operadorDominio,
    IOperadorRepositorio operadores,
    IServidorSilos servidor,
    IMigradorSilo migrador,
    IAlmacenSecretos secretos,
    IGeneradorSecretos generador,
    TimeProvider reloj) : ICasoUsoConRetorno<AprovisionarOperadorDominio, ResultadoAprovisionamiento>
{
    public async Task<ResultadoAprovisionamiento> EjecutarAsync(AprovisionarOperadorDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        // 1. Reglas que consultan el catálogo, antes de crear cualquier recurso.
        OperadorDominio.ValidarIdentificador(datos.TenantId);
        await operadorDominio.ValidarNitDisponibleAsync(datos.Nit, datos.TenantId, ct);
        var operador = await operadorDominio.ConsultarAsync(datos.TenantId, ct);

        // 2. Catálogo: el operador queda registrado antes de crear recursos, para que un fallo sea visible y reintentable.
        var operadorNuevo = operador is null;
        if (operador is null)
        {
            operador = new Operador(datos.TenantId, datos.RazonSocial, datos.Nit, datos.CorreoContacto, reloj.GetUtcNow());
            await operadores.CrearAsync(operador, ct);
        }
        else
        {
            OperadorDominio.ValidarNoSuspendido(operador);
            operador.ActualizarDatos(datos.RazonSocial, datos.Nit, datos.CorreoContacto);
            await operadores.ModificarAsync(operador.Id, operador, ct);
        }

        // 3. Secreto: se reutiliza la contraseña guardada; solo se genera una si el operador aún no tiene.
        var contrasena = await secretos.ObtenerAsync(operador.AmbitoSecretos, ClavesSecretos.ContrasenaSilo, ct);
        var contrasenaNueva = contrasena is null;
        if (contrasena is null)
        {
            contrasena = generador.NuevaContrasena();
            await secretos.GuardarAsync(operador.AmbitoSecretos, ClavesSecretos.ContrasenaSilo, contrasena, ct);
        }

        // 4. Base y rol propios del Silo, con la contraseña del secreto.
        await servidor.AsegurarSiloAsync(operador.NombreSilo, contrasena, ct);
        var cadenaConexion = servidor.CadenaConexion(operador.NombreSilo, contrasena);
        await secretos.GuardarAsync(operador.AmbitoSecretos, ClavesSecretos.CadenaConexionSilo, cadenaConexion, ct);

        // 5. Esquema operativo al día y operador listo para operar.
        var migraciones = await migrador.MigrarAsync(cadenaConexion, ct);
        operador.Activar();
        await operadores.ModificarAsync(operador.Id, operador, ct);

        return new ResultadoAprovisionamiento(operador, operadorNuevo, contrasenaNueva, migraciones);
    }
}
