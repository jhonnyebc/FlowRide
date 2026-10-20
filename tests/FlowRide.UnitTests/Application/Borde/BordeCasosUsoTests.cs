using FlowRide.Application.Borde.CasosUso;
using FlowRide.Application.Borde.Entrada.Adaptadores;
using FlowRide.Application.Borde.Entrada.Dto;
using FlowRide.Application.Borde.Salida.Puertos;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Borde;
using FlowRide.Domain.Plataforma.Repositorios;
using FlowRide.Domain.Transversal;
using FlowRide.UnitTests.Application.Tenancy;

namespace FlowRide.UnitTests.Application.Borde;

internal sealed class RutasEnMemoria(OperadoresEnMemoria operadores) : IRutaOperadorRepositorio
{
    public Dictionary<string, RutaOperador> Rutas { get; } = new(StringComparer.Ordinal);
    public int Modificadas { get; private set; }

    public Task CrearAsync(RutaOperador entidad, CancellationToken ct)
    {
        Rutas.Add(entidad.OperadorId, entidad);
        return Task.CompletedTask;
    }

    public Task ModificarAsync(string id, RutaOperador entidad, CancellationToken ct)
    {
        Rutas[id] = entidad;
        Modificadas++;
        return Task.CompletedTask;
    }

    public Task<RutaOperador?> ConsultarPorIdAsync(string id, CancellationToken ct) => Task.FromResult(Rutas.GetValueOrDefault(id));

    public Task<IReadOnlyList<RutaOperador>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RutaOperador>>(Rutas.Values.ToList());

    public Task<RutaOperador?> ConsultarPorSubdominioAsync(string subdominio, CancellationToken ct) =>
        Task.FromResult(Rutas.Values.FirstOrDefault(r => r.Subdominio == subdominio));

    public Task<RutaOperador?> ConsultarPorNumeroAsync(string phoneNumberId, CancellationToken ct) =>
        Task.FromResult(Rutas.Values.FirstOrDefault(r => r.PhoneNumberId == phoneNumberId));

    public Task<IReadOnlyList<RutaOperador>> ConsultarDeOperadoresActivosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RutaOperador>>(Rutas.Values
            .Where(r => operadores.Operadores.GetValueOrDefault(r.OperadorId)?.Estado == EstadoOperador.Activo).ToList());
}

internal sealed class VerificadorFalso : IVerificadorWebhook
{
    public const string FirmaBuena = "sha256=buena";
    public Dictionary<string, IReadOnlyList<string>> Numeros { get; } = new(StringComparer.Ordinal);

    public bool VerificarSuscripcion(string? modo, string? token) => modo == "subscribe" && token == "secreto";

    public bool FirmaValida(string cuerpo, string? firma) => firma == FirmaBuena;

    public IReadOnlyList<string> NumerosDestino(string cuerpo) =>
        Numeros.TryGetValue(cuerpo, out var numeros) ? numeros : throw new FormatException("No es un webhook.");
}

public class BordeCasosUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly OperadoresEnMemoria _operadores = new();
    private readonly RutasEnMemoria _rutas;
    private readonly VerificadorFalso _verificador = new();

    public BordeCasosUsoTests()
    {
        _rutas = new RutasEnMemoria(_operadores);
        foreach (var id in new[] { "tenant-a", "tenant-b" })
        {
            var operador = new Operador(id, $"Transportes {id}", $"nit-{id}", $"{id}@ops.co", Ahora);
            operador.Activar();
            _operadores.Operadores[id] = operador;
        }
    }

    private RegistrarRutaOperadorCasoUso Registrar => new(_operadores, _rutas, new RelojFijo(Ahora));

    private RutasOperadorAdaptadorEntrada AdaptadorRutas => new(Registrar, new ConsultarRutasOperadorCasoUso(_rutas));

    private BordeAdaptadorEntrada AdaptadorBorde =>
        new(new CargarTablaRutasCasoUso(_rutas), new EnrutarWebhookCasoUso(_verificador), _verificador);

    [Fact]
    public async Task RegistrarRuta_CreaYLuegoActualiza_ConElIdComoSubdominioPorDefecto()
    {
        var creada = await AdaptadorRutas.RegistrarAsync(new RegistrarRutaOperadorPeticion { TenantId = " tenant-a ", UrlStack = "http://localhost:5237" },
            CancellationToken.None);
        var actualizada = await AdaptadorRutas.RegistrarAsync(new RegistrarRutaOperadorPeticion
        {
            TenantId = "tenant-a",
            UrlStack = "http://localhost:6000",
            Subdominio = "aerotaxi",
            PhoneNumberId = "123456789"
        }, CancellationToken.None);

        Assert.Equal(new RutaOperadorRespuesta("tenant-a", "tenant-a", "http://localhost:5237", null), creada);
        Assert.Equal(new RutaOperadorRespuesta("tenant-a", "aerotaxi", "http://localhost:6000", "123456789"), actualizada);
        Assert.Equal(1, _rutas.Modificadas);
        Assert.Single(await AdaptadorRutas.ConsultarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RegistrarRuta_NoPermiteSubdominioNiNumeroDeOtroOperador()
    {
        await Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-a", "tenant-a", "http://a", "11111"), CancellationToken.None);

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-b", "tenant-a", "http://b", null), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-b", "tenant-b", "http://b", "11111"), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-z", "tenant-z", "http://z", null), CancellationToken.None));
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            AdaptadorRutas.RegistrarAsync(new RegistrarRutaOperadorPeticion { TenantId = "tenant-b" }, CancellationToken.None));
        Assert.Single(_rutas.Rutas);
    }

    [Fact]
    public async Task CargarRutas_SoloPublicaLosOperadoresActivos()
    {
        await Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-a", "tenant-a", "http://a:5000", "11111"), CancellationToken.None);
        await Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-b", "tenant-b", "http://b:5000", null), CancellationToken.None);
        _operadores.Operadores["tenant-b"].Suspender();

        var vigentes = await AdaptadorBorde.CargarRutasAsync("dev.localhost", CancellationToken.None);

        Assert.Equal(new RutaBordeRespuesta("tenant-a", "tenant-a.dev.localhost", new Uri("http://a:5000")), Assert.Single(vigentes.Rutas));
        Assert.Equal("dev.localhost", vigentes.DominioBase);
        Assert.Empty(RutasVigentes.Vacias("dev.localhost").Rutas);
    }

    [Fact]
    public async Task EnrutarWebhook_ValidaFirmaYFormato_YAgrupaPorOperador()
    {
        await Registrar.EjecutarAsync(new RegistrarRutaOperadorDominio("tenant-a", "tenant-a", "http://a:5000", "11111"), CancellationToken.None);
        var vigentes = await AdaptadorBorde.CargarRutasAsync("dev.localhost", CancellationToken.None);
        _verificador.Numeros["varios"] = ["11111", "11111", "99999"];

        var sinFirma = await AdaptadorBorde.EnrutarWebhookAsync("varios", "sha256=mala", vigentes, CancellationToken.None);
        var roto = await AdaptadorBorde.EnrutarWebhookAsync("{roto", VerificadorFalso.FirmaBuena, vigentes, CancellationToken.None);
        var valido = await AdaptadorBorde.EnrutarWebhookAsync("varios", VerificadorFalso.FirmaBuena, vigentes, CancellationToken.None);

        Assert.Equal((false, false), (sinFirma.FirmaValida, sinFirma.FormatoValido));
        Assert.Equal((true, false), (roto.FirmaValida, roto.FormatoValido));
        Assert.Empty(roto.Destinos);
        Assert.Equal(new DestinoWebhook("tenant-a", new Uri("http://a:5000")), Assert.Single(valido.Destinos));
        Assert.Equal(["99999"], valido.NumerosSinOperador);
        Assert.True(AdaptadorBorde.VerificarSuscripcion("subscribe", "secreto"));
        Assert.False(AdaptadorBorde.VerificarSuscripcion("subscribe", "otro"));
    }
}
