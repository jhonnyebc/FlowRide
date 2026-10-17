using FlowRide.Application.Comun.Salida.Puertos;
using FlowRide.Application.Identidad.Salida.Puertos;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Auditoria.Repositorios;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Identidad.Repositorios;

namespace FlowRide.UnitTests.Application.Identidad;

/// <summary>Dobles en memoria de los puertos de salida de Identidad y Auditoría; registran lo que se les pidió.</summary>
internal sealed class UsuariosEnMemoria : IUsuarioRepositorio
{
    private static readonly DateTimeOffset Creacion = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public Dictionary<Guid, Usuario> Usuarios { get; } = [];
    public int Creados { get; private set; }
    public int Modificados { get; private set; }

    public Task CrearAsync(Usuario entidad, CancellationToken ct)
    {
        Usuarios.Add(entidad.Id, entidad);
        Creados++;
        return Task.CompletedTask;
    }

    public Task ModificarAsync(Guid id, Usuario entidad, CancellationToken ct)
    {
        Usuarios[id] = entidad;
        Modificados++;
        return Task.CompletedTask;
    }

    public Task<Usuario?> ConsultarPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Usuarios.GetValueOrDefault(id));

    public Task<IReadOnlyList<Usuario>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Usuario>>(Usuarios.Values.ToList());

    public Task<Usuario?> ConsultarPorCorreoAsync(string correo, CancellationToken ct) =>
        Task.FromResult(Usuarios.Values.FirstOrDefault(usuario => usuario.Correo == correo));

    public Task<Usuario?> ConsultarPorTelefonoAsync(string telefonoWhatsApp, CancellationToken ct) =>
        Task.FromResult(Usuarios.Values.FirstOrDefault(usuario => usuario.TelefonoWhatsApp == telefonoWhatsApp));

    public Task<Usuario?> ConsultarPorIdentidadExternaAsync(string identidadExternaId, CancellationToken ct) =>
        Task.FromResult(Usuarios.Values.FirstOrDefault(usuario => usuario.IdentidadExternaId == identidadExternaId));

    public Usuario Registrar(string nombre, string? correo, string telefono, Guid? rolId = null, bool activo = true)
    {
        var rol = rolId ?? RolesSistema.AdministradorId;
        var usuario = new Usuario(Guid.NewGuid(), nombre, telefono, rol, Creacion, correo,
            RolesSistema.EsDelPanel(rol) ? $"sub-{correo}" : null);
        if (!activo) usuario.Desactivar();
        Usuarios[usuario.Id] = usuario;
        return usuario;
    }
}

internal sealed class AuditoriaEnMemoria : IEventoAuditoriaRepositorio
{
    public List<EventoAuditoria> Pendientes { get; } = [];
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task CrearAsync(EventoAuditoria entidad, CancellationToken ct)
    {
        Pendientes.Add(entidad);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ConsultarEnOrdenAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EventoAuditoria>>(Eventos.ToList());
}

/// <summary>Sella y confirma los eventos pendientes, como la unidad de trabajo real.</summary>
internal sealed class UnidadTrabajoEnMemoria(AuditoriaEnMemoria auditoria) : IUnidadTrabajo
{
    public int Confirmaciones { get; private set; }
    public bool Falla { get; set; }

    public Task GuardarCambiosAsync(CancellationToken ct)
    {
        if (Falla)
            throw new InvalidOperationException("La base no responde.");

        foreach (var evento in auditoria.Pendientes)
        {
            evento.Sellar(auditoria.Eventos.LastOrDefault()?.Hash ?? EventoAuditoria.HashInicial);
            auditoria.Eventos.Add(evento);
        }
        auditoria.Pendientes.Clear();
        Confirmaciones++;
        return Task.CompletedTask;
    }
}

internal sealed class ProveedorEnMemoria : IProveedorIdentidad
{
    public Dictionary<string, (string Id, string Nombre)> Cuentas { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, bool> Habilitadas { get; } = new(StringComparer.Ordinal);
    public List<string> CorreosEnviados { get; } = [];
    public int Llamadas { get; private set; }

    public Task<string> AsegurarUsuarioAsync(string correo, string nombre, CancellationToken ct)
    {
        Llamadas++;
        if (!Cuentas.TryGetValue(correo, out var cuenta))
        {
            cuenta = ($"sub-{correo}", nombre);
            Cuentas[correo] = cuenta;
            Habilitadas[cuenta.Id] = true;
        }
        return Task.FromResult(cuenta.Id);
    }

    public Task EnviarConfiguracionContrasenaAsync(string identidadExternaId, CancellationToken ct)
    {
        Llamadas++;
        CorreosEnviados.Add(identidadExternaId);
        return Task.CompletedTask;
    }

    public Task HabilitarAsync(string identidadExternaId, CancellationToken ct)
    {
        Llamadas++;
        Habilitadas[identidadExternaId] = true;
        return Task.CompletedTask;
    }

    public Task DeshabilitarAsync(string identidadExternaId, CancellationToken ct)
    {
        Llamadas++;
        Habilitadas[identidadExternaId] = false;
        return Task.CompletedTask;
    }
}
