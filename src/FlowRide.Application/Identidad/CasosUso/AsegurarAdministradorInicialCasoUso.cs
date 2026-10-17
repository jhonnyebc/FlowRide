using FlowRide.Application.Comun.CasosUso;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.GestionUsuarios;
using FlowRide.Domain.Identidad.Repositorios;
using FlowRide.Domain.Transversal;

namespace FlowRide.Application.Identidad.CasosUso;

public sealed record ResultadoAdministradorInicial(Usuario Usuario, bool Nuevo);

/// <summary>
/// Primer Administrador del operador, creado por la plataforma al aprovisionarlo (ADR-0010). Es idempotente: si ya
/// existe un administrador con ese correo, no hace nada. Reutiliza el alta normal de usuarios.
/// </summary>
public sealed class AsegurarAdministradorInicialCasoUso(
    IUsuarioRepositorio usuarios,
    ICasoUsoConRetorno<CrearUsuarioDominio, Usuario> crearUsuario)
    : ICasoUsoConRetorno<CrearUsuarioDominio, ResultadoAdministradorInicial>
{
    public async Task<ResultadoAdministradorInicial> EjecutarAsync(CrearUsuarioDominio datos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var existente = await usuarios.ConsultarPorCorreoAsync(datos.Correo.Trim(), ct);
        if (existente is not null)
        {
            if (existente.RolId != RolesSistema.AdministradorId)
                throw new FlowRideExcepcion(
                    $"El correo {datos.Correo.Trim()} ya pertenece a un usuario con rol {RolesSistema.NombrePorId(existente.RolId)}.");
            return new ResultadoAdministradorInicial(existente, Nuevo: false);
        }

        var usuario = await crearUsuario.EjecutarAsync(datos with { Rol = RolesSistema.Administrador, ActorId = null }, ct);
        return new ResultadoAdministradorInicial(usuario, Nuevo: true);
    }
}
