using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FlowRide.Application.Auditoria.Entrada.Dto;
using FlowRide.Application.Identidad.Entrada.Dto;
using FlowRide.Domain.Auditoria;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FlowRide.IntegrationTests.Identidad;

/// <summary>
/// RF-01, RF-02 y ADM-SEG-01 de punta a punta en la API real, con tokens de un emisor de prueba: autenticación,
/// tenant del token, usuario interno activo en cada petición, RBAC y auditoría de la gestión de usuarios.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionPostgres.Nombre)]
public class AutorizacionApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Tenant = "api-identidad";
    private static readonly DateTimeOffset Ahora = new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private EntornoAprovisionamiento _entorno = null!;
    private FlowRideApiFactory _api = null!;
    private readonly ProveedorIdentidadDePrueba _proveedor = new();

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres);
        await _entorno.AprovisionarAsync(Tenant);
        _api = new FlowRideApiFactory(await _entorno.CadenaDelSiloAsync(Tenant), Tenant)
        {
            ConTokensDePrueba = true,
            Proveedor = _proveedor
        };
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _entorno.DisposeAsync();
    }

    /// <summary>Usuario interno con su cuenta del Panel Web (sub) y un token vigente del realm.</summary>
    private async Task<(Usuario Usuario, HttpClient Cliente)> UsuarioConSesionAsync(Guid rolId, bool activo = true)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..10];
        var usuario = new Usuario(Guid.NewGuid(), $"Usuario {sufijo}", $"+57{sufijo[..8]}", rolId, Ahora,
            $"{sufijo}@ops.co", $"sub-{sufijo}");
        if (!activo) usuario.Desactivar();

        await using (var silo = await _entorno.AbrirSiloAsync(Tenant))
        {
            silo.Contexto.Usuarios.Add(usuario);
            await silo.Contexto.SaveChangesAsync();
        }

        return (usuario, Cliente(TokensDePrueba.Crear(usuario.IdentidadExternaId!, Tenant)));
    }

    private HttpClient Cliente(string? token)
    {
        var cliente = _api.CreateClient();
        if (token is not null) cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [Fact]
    public async Task SinToken_Responde401()
    {
        var respuesta = await Cliente(null).GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task LaSaludYLasRutasInexistentes_NoPidenToken()
    {
        Assert.Equal(HttpStatusCode.OK, (await Cliente(null).GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente(null).GetAsync("/no-existe")).StatusCode);
    }

    [Fact]
    public async Task TokenValidoDeOtroOperador_Responde401YQuedaEnElLog()
    {
        var (usuario, _) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);

        var respuesta = await Cliente(TokensDePrueba.Crear(usuario.IdentidadExternaId!, "otro-operador")).GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains(_api.Registro.Entradas, entrada => entrada.EventId.Id == 2001);
    }

    public static TheoryData<string> TokensInvalidos => new()
    {
        "sin-tenant",
        "expirado",
        "otra-audiencia",
        "otra-firma"
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task TokenInvalido_Responde401(string caso)
    {
        var (usuario, _) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);
        var sub = usuario.IdentidadExternaId!;
        var token = caso switch
        {
            "sin-tenant" => TokensDePrueba.Crear(sub, tenantId: null),
            "expirado" => TokensDePrueba.Crear(sub, Tenant, expira: DateTime.UtcNow.AddMinutes(-1)),
            "otra-audiencia" => TokensDePrueba.Crear(sub, Tenant, audiencia: "account"),
            _ => TokensDePrueba.Crear(sub, Tenant, clave: new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)))
        };

        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente(token).GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task UsuarioDesconocidoOInactivo_Responde401AunqueElTokenSeaValido()
    {
        var (_, inactivo) = await UsuarioConSesionAsync(RolesSistema.AdministradorId, activo: false);
        var desconocido = Cliente(TokensDePrueba.Crear("sub-que-no-existe", Tenant));

        Assert.Equal(HttpStatusCode.Unauthorized, (await inactivo.GetAsync("/api/usuarios/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await desconocido.GetAsync("/api/usuarios/yo")).StatusCode);
        Assert.Contains(_api.Registro.Entradas, entrada => entrada.EventId.Id == 2002);
    }

    [Fact]
    public async Task OperadorLogistico_VeSuPerfilPeroNoAdministraUsuarios()
    {
        var (usuario, cliente) = await UsuarioConSesionAsync(RolesSistema.OperadorLogisticoId);

        var yo = await cliente.GetFromJsonAsync<UsuarioAutenticadoRespuesta>("/api/usuarios/yo");

        Assert.Equal(new UsuarioAutenticadoRespuesta(usuario.Id, usuario.Nombre, usuario.Correo, RolesSistema.OperadorLogistico), yo);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/usuarios", new CrearUsuarioPeticion())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/auditoria/verificacion")).StatusCode);
    }

    [Fact]
    public async Task PasajerosYConductores_NoUsanElPanelWeb()
    {
        var (_, conductor) = await UsuarioConSesionAsync(RolesSistema.ConductorId);

        Assert.Equal(HttpStatusCode.Forbidden, (await conductor.GetAsync("/api/usuarios/yo")).StatusCode);
    }

    [Fact]
    public async Task Administrador_CreaUnUsuario_ConSuCuentaCorreoYAuditoria()
    {
        var (administrador, cliente) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);
        var correo = $"{Guid.NewGuid():N}"[..10] + "@ops.co";

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios", new CrearUsuarioPeticion
        {
            Nombre = "Laura Gómez",
            Correo = correo,
            TelefonoWhatsApp = $"+57{Random.Shared.Next(100_000_000, 999_999_999)}",
            Rol = RolesSistema.OperadorLogistico
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creado = (await respuesta.Content.ReadFromJsonAsync<UsuarioRespuesta>())!;
        Assert.Equal($"/api/usuarios/{creado.Id}", respuesta.Headers.Location?.OriginalString);
        Assert.Equal(RolesSistema.OperadorLogistico, creado.Rol);
        Assert.Contains($"sub-{correo}", _proveedor.CorreosEnviados);

        var listado = await cliente.GetFromJsonAsync<List<UsuarioRespuesta>>("/api/usuarios?rol=OperadorLogistico");
        Assert.Contains(listado!, usuario => usuario.Id == creado.Id);

        await using var silo = await _entorno.AbrirSiloAsync(Tenant);
        var evento = await silo.Contexto.EventosAuditoria.SingleAsync(e => e.RecursoId == creado.Id.ToString());
        Assert.Equal(AccionesAuditoria.UsuarioCrear, evento.Accion);
        Assert.Equal(administrador.Id, evento.ActorId);
    }

    [Fact]
    public async Task ReglaDeNegocioIncumplida_Responde400ConElMotivo()
    {
        var (administrador, cliente) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);

        var rolInvalido = await cliente.PostAsJsonAsync("/api/usuarios", new CrearUsuarioPeticion
        {
            Nombre = "X",
            Correo = "x@ops.co",
            TelefonoWhatsApp = "+57123",
            Rol = RolesSistema.Conductor
        });
        var aSiMismo = await cliente.PostAsync($"/api/usuarios/{administrador.Id}/desactivar", null);

        Assert.Equal(HttpStatusCode.BadRequest, rolInvalido.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, aSiMismo.StatusCode);
        Assert.Contains("propio estado", (await aSiMismo.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DesactivarUnUsuario_RevocaSuAccesoEnLaSiguientePeticion()
    {
        var (_, administrador) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);
        var (operador, clienteDelOperador) = await UsuarioConSesionAsync(RolesSistema.OperadorLogisticoId);
        Assert.Equal(HttpStatusCode.OK, (await clienteDelOperador.GetAsync("/api/usuarios/yo")).StatusCode);

        var desactivado = await administrador.PostAsync($"/api/usuarios/{operador.Id}/desactivar", null);

        // ADM-SEG-01 (≤ 60 s): el mismo token, todavía vigente, deja de servir de inmediato.
        Assert.Equal(HttpStatusCode.OK, desactivado.StatusCode);
        Assert.False((await desactivado.Content.ReadFromJsonAsync<UsuarioRespuesta>())!.Activo);
        Assert.Equal(HttpStatusCode.Unauthorized, (await clienteDelOperador.GetAsync("/api/usuarios/yo")).StatusCode);
        Assert.False(_proveedor.Cuentas[operador.IdentidadExternaId!]);

        // Y reactivarlo le devuelve el acceso.
        Assert.Equal(HttpStatusCode.OK, (await administrador.PostAsync($"/api/usuarios/{operador.Id}/activar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteDelOperador.GetAsync("/api/usuarios/yo")).StatusCode);
    }

    [Fact]
    public async Task CambiarElRol_AplicaEnLaSiguientePeticionConElMismoToken()
    {
        var (_, administrador) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);
        var (operador, clienteDelOperador) = await UsuarioConSesionAsync(RolesSistema.OperadorLogisticoId);
        Assert.Equal(HttpStatusCode.Forbidden, (await clienteDelOperador.GetAsync("/api/usuarios")).StatusCode);

        var cambio = await administrador.PutAsJsonAsync($"/api/usuarios/{operador.Id}/rol",
            new CambiarRolUsuarioPeticion { Rol = RolesSistema.Administrador });

        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clienteDelOperador.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task VerificarLaAuditoria_TrasLaGestionDeUsuarios_EstaIntegra()
    {
        var (_, administrador) = await UsuarioConSesionAsync(RolesSistema.AdministradorId);
        var (operador, _) = await UsuarioConSesionAsync(RolesSistema.OperadorLogisticoId);
        await administrador.PostAsync($"/api/usuarios/{operador.Id}/desactivar", null);

        var verificacion = await administrador.GetFromJsonAsync<VerificacionAuditoriaRespuesta>("/api/auditoria/verificacion");

        Assert.True(verificacion!.Integra, verificacion.Mensaje);
        Assert.True(verificacion.TotalEventos >= 1);
    }
}
