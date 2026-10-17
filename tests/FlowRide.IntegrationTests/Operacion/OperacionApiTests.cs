using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FlowRide.Application.Operacion.Entrada.Dto;
using FlowRide.Application.Parametros.Entrada.Dto;
using FlowRide.Domain.Entidades;
using FlowRide.Domain.Identidad;
using FlowRide.Domain.Parametros;
using FlowRide.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FlowRide.IntegrationTests.Operacion;

/// <summary>
/// Recursos operativos y parámetros de punta a punta (RF-04, RF-05, RF-07, RF-26 a RF-28, RF-38, RF-41; ADR-0006,
/// ADR-0013, ADR-0014, ADR-0016): API real, PostgreSQL y Azurite reales, tokens de un emisor de prueba.
/// </summary>
[Trait("Category", "Integration")]
[Collection(ColeccionAzurite.Nombre)]
public class OperacionApiTests(PostgresFixture postgres, AzuriteFixture azurite) : IAsyncLifetime
{
    private static readonly DateTimeOffset Manana = DateTimeOffset.UtcNow.Date.AddDays(1).ToUniversalTime();
    private static readonly DateOnly Vigente = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));

    private readonly string _tenant = $"op-{Guid.NewGuid():N}"[..14];
    private EntornoAprovisionamiento _entorno = null!;
    private FlowRideApiFactory _api = null!;
    private HttpClient _administrador = null!;
    private HttpClient _operador = null!;

    public async Task InitializeAsync()
    {
        _entorno = await EntornoAprovisionamiento.CrearAsync(postgres, azurite: azurite);
        await _entorno.AprovisionarAsync(_tenant);
        _api = await ApiAsync();
        _administrador = await ClienteConRolAsync(_api, RolesSistema.AdministradorId);
        _operador = await ClienteConRolAsync(_api, RolesSistema.OperadorLogisticoId);
    }

    public async Task DisposeAsync()
    {
        await _api.DisposeAsync();
        await _entorno.DisposeAsync();
    }

    private async Task<FlowRideApiFactory> ApiAsync(int ttlParametrosSegundos = 60)
    {
        var ajustes = new Dictionary<string, string?>(await _entorno.AjustesDelStackAsync(_tenant))
        {
            ["FlowRide:Cache:TtlParametrosSegundos"] = ttlParametrosSegundos.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new FlowRideApiFactory(await _entorno.CadenaDelSiloAsync(_tenant), _tenant)
        {
            ConTokensDePrueba = true,
            Proveedor = new ProveedorIdentidadDePrueba(),
            Ajustes = ajustes
        };
    }

    private async Task<HttpClient> ClienteConRolAsync(FlowRideApiFactory api, Guid rolId)
    {
        var sub = $"sub-{Guid.NewGuid():N}";
        await using (var silo = await _entorno.AbrirSiloAsync(_tenant))
        {
            silo.Contexto.Usuarios.Add(new Usuario(Guid.NewGuid(), "Usuario del panel", $"+1{Random.Shared.NextInt64(1_000_000_000, 9_999_999_999)}",
                rolId, DateTimeOffset.UtcNow, null, sub));
            await silo.Contexto.SaveChangesAsync();
        }

        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokensDePrueba.Crear(sub, _tenant));
        return cliente;
    }

    private static string Telefono() => $"+57{Random.Shared.NextInt64(3_000_000_000, 3_999_999_999)}";

    private async Task<ConductorRespuesta> RegistrarConductorAsync(string? licencia = null, string? telefono = null)
    {
        var respuesta = await _administrador.PostAsJsonAsync("/api/conductores", new ConductorPeticion
        {
            Nombre = "Luis Pérez",
            TelefonoWhatsApp = telefono ?? Telefono(),
            NumeroLicencia = licencia ?? Random.Shared.NextInt64(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
            CategoriaLicencia = "C2",
            VencimientoLicencia = Vigente
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<ConductorRespuesta>())!;
    }

    private async Task<string> ValorEnLaBaseAsync(string sql)
    {
        var baseDelSilo = new NpgsqlConnectionStringBuilder(_entorno.CadenaAdministracion) { Database = $"flowride_{_tenant.Replace('-', '_')}" };
        await using var fuente = NpgsqlDataSource.Create(baseDelSilo.ConnectionString);
        return (string)(await fuente.CreateCommand(sql).ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task EmpresaYPasajero_SeRegistranBuscanYAuditan()
    {
        var empresa = await (await _administrador.PostAsJsonAsync("/api/empresas-cliente",
            new EmpresaClientePeticion { Nombre = "Aerolínea del Oriente", Nit = "900555666-7", CorreoContacto = "ops@aero.co" }))
            .Content.ReadFromJsonAsync<EmpresaClienteRespuesta>();
        var telefono = Telefono();
        var creado = await _administrador.PostAsJsonAsync("/api/pasajeros", new PasajeroPeticion
        {
            Nombre = "Ana Gómez",
            TelefonoWhatsApp = telefono,
            EmpresaClienteId = empresa!.Id,
            CodigoInterno = "E-100"
        });
        var repetido = await _administrador.PostAsJsonAsync("/api/pasajeros", new PasajeroPeticion
        {
            Nombre = "Otra",
            TelefonoWhatsApp = Telefono(),
            EmpresaClienteId = empresa.Id,
            CodigoInterno = "E-100"
        });

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        var porNombre = await _operador.GetFromJsonAsync<PaginaRespuesta<PasajeroRespuesta>>("/api/pasajeros?texto=gómez");
        var porTelefono = await _operador.GetFromJsonAsync<PaginaRespuesta<PasajeroRespuesta>>($"/api/pasajeros?texto={Uri.EscapeDataString(telefono)}");
        var empresas = await _operador.GetFromJsonAsync<PaginaRespuesta<EmpresaClienteRespuesta>>("/api/empresas-cliente?texto=oriente&activo=true");
        Assert.Equal("Ana Gómez", Assert.Single(porNombre!.Elementos).Nombre);
        Assert.Equal(telefono, Assert.Single(porTelefono!.Elementos).TelefonoWhatsApp);
        Assert.Equal(empresa.Id, Assert.Single(empresas!.Elementos).Id);

        await using var silo = await _entorno.AbrirSiloAsync(_tenant);
        Assert.Contains(await silo.Contexto.EventosAuditoria.Select(e => e.Accion).ToListAsync(), a => a == "pasajero.crear");
    }

    [Fact]
    public async Task TelefonoYLicencia_SeGuardanCifrados_PeroSeBuscanPorValorExacto()
    {
        var telefono = Telefono();
        var conductor = await RegistrarConductorAsync("79111222", telefono);

        var telefonoEnBase = await ValorEnLaBaseAsync($"SELECT telefono_whats_app FROM ops.usuario WHERE id = '{conductor.Id}'");
        var licenciaEnBase = await ValorEnLaBaseAsync($"SELECT numero_licencia FROM ops.conductor WHERE id = '{conductor.Id}'");
        var porLicencia = await _operador.GetFromJsonAsync<PaginaRespuesta<ConductorRespuesta>>("/api/conductores?texto=79111222");

        Assert.StartsWith("v1.", telefonoEnBase, StringComparison.Ordinal);
        Assert.DoesNotContain(telefono[3..], telefonoEnBase, StringComparison.Ordinal);
        Assert.StartsWith("v1.", licenciaEnBase, StringComparison.Ordinal);
        Assert.DoesNotContain("79111222", licenciaEnBase, StringComparison.Ordinal);
        Assert.Equal(conductor.Id, Assert.Single(porLicencia!.Elementos).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await _administrador.PostAsJsonAsync("/api/conductores", new ConductorPeticion
        {
            Nombre = "Otro",
            TelefonoWhatsApp = Telefono(),
            NumeroLicencia = "79111222",
            CategoriaLicencia = "C1",
            VencimientoLicencia = Vigente
        })).StatusCode);
    }

    [Fact]
    public async Task OperadorLogistico_ConsultaYProgramaTurnos_PeroNoAdministraRecursos()
    {
        var conductor = await RegistrarConductorAsync();

        Assert.Equal(HttpStatusCode.OK, (await _operador.GetAsync("/api/conductores")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _operador.GetAsync($"/api/conductores/{conductor.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _operador.PostAsJsonAsync($"/api/conductores/{conductor.Id}/turnos",
            new TurnoPeticion { Inicio = Manana.AddHours(6), Fin = Manana.AddHours(14) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _operador.PostAsJsonAsync("/api/vehiculos", new VehiculoPeticion())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _operador.PutAsJsonAsync($"/api/conductores/{conductor.Id}/estado",
            new EstadoConductorPeticion { Estado = "Inactivo" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _operador.PutAsJsonAsync($"/api/parametros/{CatalogoParametros.VentanaCancelacionMinutos}",
            new CambiarParametroPeticion { Valor = JsonDocument.Parse("10").RootElement })).StatusCode);
    }

    [Fact]
    public async Task Disponibilidad_SigueLosTurnosYElEstadoOperativo()
    {
        var conductor = await RegistrarConductorAsync();
        await _administrador.PostAsJsonAsync($"/api/conductores/{conductor.Id}/turnos",
            new TurnoPeticion { Inicio = Manana.AddHours(6), Fin = Manana.AddHours(14) });
        var solapado = await _administrador.PostAsJsonAsync($"/api/conductores/{conductor.Id}/turnos",
            new TurnoPeticion { Inicio = Manana.AddHours(10), Fin = Manana.AddHours(18) });
        var consulta = $"/api/conductores/disponibles?inicio={Uri.EscapeDataString(Manana.AddHours(8).ToString("O"))}" +
                       $"&fin={Uri.EscapeDataString(Manana.AddHours(9).ToString("O"))}";

        var antes = await _operador.GetFromJsonAsync<List<ConductorDisponibleRespuesta>>(consulta);
        await _administrador.PutAsJsonAsync($"/api/conductores/{conductor.Id}/estado", new EstadoConductorPeticion { Estado = "Suspendido" });
        var despues = await _operador.GetFromJsonAsync<List<ConductorDisponibleRespuesta>>(consulta);

        Assert.Equal(HttpStatusCode.BadRequest, solapado.StatusCode);
        Assert.Contains(antes!, c => c.Id == conductor.Id);
        Assert.DoesNotContain(despues!, c => c.Id == conductor.Id);
    }

    [Fact]
    public async Task Vehiculo_ConConductorHabitual_YBusquedaPorPlaca()
    {
        var conductor = await RegistrarConductorAsync();
        var placa = $"T{Random.Shared.Next(10_000, 99_999)}";
        var vehiculo = await (await _administrador.PostAsJsonAsync("/api/vehiculos", new VehiculoPeticion
        {
            Placa = placa.ToLowerInvariant(),
            Marca = "Toyota",
            Modelo = "Hiace",
            Capacidad = 12,
            VencimientoRtm = Vigente,
            VencimientoSoat = Vigente
        })).Content.ReadFromJsonAsync<VehiculoRespuesta>();

        var asignado = await _administrador.PutAsJsonAsync($"/api/vehiculos/{vehiculo!.Id}/conductor-habitual",
            new ConductorHabitualPeticion { ConductorId = conductor.Id });
        var encontrados = await _operador.GetFromJsonAsync<PaginaRespuesta<VehiculoRespuesta>>($"/api/vehiculos?texto={placa[..4]}");
        var desactivado = await _administrador.PostAsync($"/api/vehiculos/{vehiculo.Id}/desactivar", null);

        Assert.Equal(placa, vehiculo.Placa);
        Assert.Equal(conductor.Id, (await asignado.Content.ReadFromJsonAsync<VehiculoRespuesta>())!.ConductorHabitualId);
        Assert.Contains(encontrados!.Elementos, v => v.Id == vehiculo.Id);
        Assert.False((await desactivado.Content.ReadFromJsonAsync<VehiculoRespuesta>())!.Activo);
    }

    [Fact]
    public async Task Documento_SeSubeAlBucket_SeDescargaConUrlFirmada_YSeElimina()
    {
        var conductor = await RegistrarConductorAsync();
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.7\nlicencia de prueba\n%%EOF");
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent("Licencia"), "tipo" },
            { new StringContent(Vigente.ToString("yyyy-MM-dd")), "fechaVencimiento" },
            { new ByteArrayContent(pdf) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "archivo", "licencia.pdf" }
        };

        var subida = await _administrador.PostAsync($"/api/conductores/{conductor.Id}/documentos", formulario);
        var documento = (await subida.Content.ReadFromJsonAsync<DocumentoConductorRespuesta>())!;
        var url = (await _operador.GetFromJsonAsync<UrlDocumentoRespuesta>(
            $"/api/conductores/{conductor.Id}/documentos/{documento.Id}/descarga"))!;
        using var descarga = new HttpClient();
        var descargado = await descarga.GetByteArrayAsync(url.Url);

        Assert.Equal(HttpStatusCode.Created, subida.StatusCode);
        Assert.Contains("sig=", url.Url.Query, StringComparison.Ordinal);
        Assert.True(url.ExpiraEn <= DateTimeOffset.UtcNow.AddMinutes(15).AddSeconds(5));
        Assert.Equal(pdf, descargado);
        // El bucket es privado: sin la firma, la misma ruta no entrega el archivo.
        Assert.NotEqual(HttpStatusCode.OK, (await descarga.GetAsync(url.Url.GetLeftPart(UriPartial.Path))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _administrador.DeleteAsync($"/api/conductores/{conductor.Id}/documentos/{documento.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await descarga.GetAsync(url.Url)).StatusCode);
    }

    [Fact]
    public async Task Documento_ConContenidoQueNoCorrespondeAlTipo_SeRechaza()
    {
        var conductor = await RegistrarConductorAsync();
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent("Licencia"), "tipo" },
            { new ByteArrayContent([0x4D, 0x5A, 0x90, 0x00]) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } }, "archivo", "virus.pdf" }
        };

        var respuesta = await _administrador.PostAsync($"/api/conductores/{conductor.Id}/documentos", formulario);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Parametros_PorDefecto_CambioVersionado_YValidado()
    {
        var clave = CatalogoParametros.VentanaCancelacionMinutos;

        var iniciales = await _operador.GetFromJsonAsync<List<ParametroRespuesta>>("/api/parametros");
        var cambio = await _administrador.PutAsJsonAsync($"/api/parametros/{clave}",
            new CambiarParametroPeticion { Valor = JsonDocument.Parse("45").RootElement });
        var invalido = await _administrador.PutAsJsonAsync($"/api/parametros/{clave}",
            new CambiarParametroPeticion { Valor = JsonDocument.Parse("5000").RootElement });

        var inicial = iniciales!.Single(p => p.Clave == clave);
        Assert.Equal((30, 0, true), (inicial.Valor.GetInt32(), inicial.Version, inicial.EsPorDefecto));
        var cambiado = (await cambio.Content.ReadFromJsonAsync<ParametroRespuesta>())!;
        Assert.Equal((45, 1, false), (cambiado.Valor.GetInt32(), cambiado.Version, cambiado.EsPorDefecto));
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.Equal(45, (await _operador.GetFromJsonAsync<List<ParametroRespuesta>>("/api/parametros"))!.Single(p => p.Clave == clave).Valor.GetInt32());

        await using var silo = await _entorno.AbrirSiloAsync(_tenant);
        var evento = await silo.Contexto.EventosAuditoria.SingleAsync(e => e.Accion == "parametro.actualizar");
        Assert.Equal(clave, evento.RecursoId);
    }

    [Fact]
    public async Task OtraReplica_VeElCambioAlVencerSuCache()
    {
        var clave = CatalogoParametros.AnticipacionRecordatorioMinutos;
        await using var otraReplica = await ApiAsync(ttlParametrosSegundos: 1);
        var clienteDeLaOtra = await ClienteConRolAsync(otraReplica, RolesSistema.OperadorLogisticoId);
        async Task<int> ValorEnLaOtraAsync() =>
            (await clienteDeLaOtra.GetFromJsonAsync<List<ParametroRespuesta>>("/api/parametros"))!.Single(p => p.Clave == clave).Valor.GetInt32();

        Assert.Equal(60, await ValorEnLaOtraAsync());
        await _administrador.PutAsJsonAsync($"/api/parametros/{clave}", new CambiarParametroPeticion { Valor = JsonDocument.Parse("90").RootElement });

        Assert.Equal(60, await ValorEnLaOtraAsync());
        await Task.Delay(TimeSpan.FromMilliseconds(1500));
        Assert.Equal(90, await ValorEnLaOtraAsync());
    }
}
