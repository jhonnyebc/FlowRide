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
/// → migraciones → realm → clave de cifrado → bucket → operador activo. Si falla a mitad, se puede volver a ejecutar
/// con los mismos datos.
/// El primer administrador lo crea después el stack del operador; la ruta en el borde se agrega en la Fase 10.
/// </summary>
public sealed class AprovisionarOperadorCasoUso(
    OperadorDominio operadorDominio,
    IOperadorRepositorio operadores,
    IServidorSilos servidor,
    IMigradorSilo migrador,
    IServidorIdentidad servidorIdentidad,
    IServidorObjetos servidorObjetos,
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

        // 5. Esquema operativo al día (incluye los roles del sistema).
        var migraciones = await migrador.MigrarAsync(cadenaConexion, ct);

        // 6. Realm del operador en Keycloak, con el secreto de su cliente de servicio (se reutiliza si ya existe).
        var secretoCliente = await ObtenerOGenerarAsync(operador, ClavesSecretos.SecretoClienteIdentidad, ct);
        await servidorIdentidad.AsegurarRealmAsync(operador.NombreRealm, operador.Id, secretoCliente, ct);

        // 7. Clave propia del operador para cifrar los campos sensibles (ADR-0014). Nunca se reemplaza: rotarla es
        //    agregar una versión nueva.
        await ObtenerOGenerarAsync(operador, ClavesSecretos.ClaveCifradoV1, ct);
        if (await secretos.ObtenerAsync(operador.AmbitoSecretos, ClavesSecretos.VersionCifradoVigente, ct) is null)
            await secretos.GuardarAsync(operador.AmbitoSecretos, ClavesSecretos.VersionCifradoVigente, "v1", ct);

        // 8. Bucket privado del operador para sus documentos (ADR-0013).
        await servidorObjetos.AsegurarBucketAsync(operador.NombreBucket, ct);
        await secretos.GuardarAsync(operador.AmbitoSecretos, ClavesSecretos.CadenaConexionObjetos,
            servidorObjetos.CadenaConexion(operador.NombreBucket), ct);

        // 9. Operador listo para operar.
        operador.Activar();
        await operadores.ModificarAsync(operador.Id, operador, ct);

        return new ResultadoAprovisionamiento(operador, operadorNuevo, contrasenaNueva, migraciones);
    }

    /// <summary>Reutiliza el secreto guardado del operador; solo genera uno si todavía no existe.</summary>
    private async Task<string> ObtenerOGenerarAsync(Operador operador, string clave, CancellationToken ct)
    {
        var valor = await secretos.ObtenerAsync(operador.AmbitoSecretos, clave, ct);
        if (valor is not null)
            return valor;

        valor = generador.NuevaContrasena();
        await secretos.GuardarAsync(operador.AmbitoSecretos, clave, valor, ct);
        return valor;
    }
}
