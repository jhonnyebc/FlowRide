using System.Text;
using FlowRide.Application.Tenancy.CasosUso;
using FlowRide.Application.Tenancy.Entrada.Adaptadores;
using FlowRide.Application.Tenancy.Entrada.Dto;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma.Respaldos;
using FlowRide.Domain.Transversal;

namespace FlowRide.UnitTests.Application.Tenancy;

public class PoliticaRetencionRespaldosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private static Respaldo Hace(int dias) => new($"respaldos/silo-{dias}.dump", Ahora.AddDays(-dias), 100);

    [Fact]
    public void BorraLosDeMasDe35Dias_YConservaLosRecientes()
    {
        var vencidos = PoliticaRetencionRespaldos.Vencidos([Hace(0), Hace(34), Hace(35), Hace(36), Hace(90)], Ahora);

        Assert.Equal([Hace(36), Hace(90)], vencidos);
    }

    [Fact]
    public void NuncaBorraElMasReciente_AunqueEsteVencido() =>
        Assert.Equal([Hace(60)], PoliticaRetencionRespaldos.Vencidos([Hace(60), Hace(40)], Ahora));

    [Fact]
    public void ElNombre_OrdenaIgualAlfabeticaYCronologicamente_EnUtc()
    {
        var bogota = TimeSpan.FromHours(-5);
        var primero = PoliticaRetencionRespaldos.NombrePara(new DateTimeOffset(2026, 10, 31, 23, 30, 0, bogota));
        var segundo = PoliticaRetencionRespaldos.NombrePara(new DateTimeOffset(2026, 11, 1, 9, 5, 7, TimeSpan.Zero));

        Assert.Equal("respaldos/silo-20261101T043000Z.dump", primero);
        Assert.True(string.CompareOrdinal(primero, segundo) < 0);
    }
}

public class RespaldosCasosUsoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly OperadoresEnMemoria _operadores = new();
    private readonly RespaldosEnMemoria _almacen = new();

    private RespaldosOperadorAdaptadorEntrada Puerto(DateTimeOffset? ahora = null)
    {
        var reloj = new RelojFijo(ahora ?? Ahora);
        return new RespaldosOperadorAdaptadorEntrada(
            new GuardarRespaldoCasoUso(_operadores, _almacen, reloj),
            new ConsultarRespaldosCasoUso(_operadores, _almacen),
            new DescargarRespaldoCasoUso(_operadores, _almacen));
    }

    private static MemoryStream Contenido(string texto) => new(Encoding.UTF8.GetBytes(texto));

    [Fact]
    public async Task Guardar_LoDejaEnElBucketDelOperador_YAplicaLaRetencion()
    {
        _operadores.Registrar("tenant-a");
        _almacen.Agregar("flowride-tenant-a", "respaldos/silo-viejo.dump", Ahora.AddDays(-40));
        _almacen.Agregar("flowride-tenant-a", "respaldos/silo-reciente.dump", Ahora.AddDays(-2));

        var respuesta = await Puerto().GuardarAsync(new GuardarRespaldoPeticion { TenantId = " tenant-a ", Contenido = Contenido("dump") }, default);

        Assert.Equal("respaldos/silo-20261031T120000Z.dump", respuesta.Guardado.Nombre);
        Assert.Equal(4, respuesta.Guardado.TamanoBytes);
        Assert.Equal("respaldos/silo-viejo.dump", Assert.Single(respuesta.EliminadosPorRetencion).Nombre);
        Assert.Equal(35, respuesta.DiasRetencion);
        Assert.Equal(["respaldos/silo-20261031T120000Z.dump", "respaldos/silo-reciente.dump"],
            _almacen.Objetos["flowride-tenant-a"].Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task UnOperadorQueNoExiste_NoGuardaNada()
    {
        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Puerto().GuardarAsync(new GuardarRespaldoPeticion { TenantId = "tenant-x", Contenido = Contenido("dump") }, default));

        Assert.Empty(_almacen.Objetos);
    }

    [Fact]
    public async Task SinContenido_EsUnaPeticionInvalida() =>
        await Assert.ThrowsAsync<FlowRideExcepcion>(() => Puerto().GuardarAsync(new GuardarRespaldoPeticion { TenantId = "tenant-a" }, default));

    [Fact]
    public async Task Descargar_SinNombre_TraeElMasReciente_YConNombre_EseRespaldo()
    {
        _operadores.Registrar("tenant-a");
        await Puerto(Ahora.AddDays(-1)).GuardarAsync(new GuardarRespaldoPeticion { TenantId = "tenant-a", Contenido = Contenido("ayer") }, default);
        await Puerto().GuardarAsync(new GuardarRespaldoPeticion { TenantId = "tenant-a", Contenido = Contenido("hoy") }, default);

        using var reciente = new MemoryStream();
        var masReciente = await Puerto().DescargarAsync(new DescargarRespaldoPeticion { TenantId = "tenant-a", Destino = reciente }, default);
        using var elDeAyer = new MemoryStream();
        await Puerto().DescargarAsync(new DescargarRespaldoPeticion
        {
            TenantId = "tenant-a",
            Nombre = "respaldos/silo-20261030T120000Z.dump",
            Destino = elDeAyer
        }, default);

        Assert.Equal("respaldos/silo-20261031T120000Z.dump", masReciente.Nombre);
        Assert.Equal("hoy", Encoding.UTF8.GetString(reciente.ToArray()));
        Assert.Equal("ayer", Encoding.UTF8.GetString(elDeAyer.ToArray()));
        Assert.Equal(2, (await Puerto().ConsultarAsync("tenant-a", default)).Count);
    }

    [Fact]
    public async Task Descargar_SinRespaldos_Falla()
    {
        _operadores.Registrar("tenant-a");

        await Assert.ThrowsAsync<FlowRideExcepcion>(() =>
            Puerto().DescargarAsync(new DescargarRespaldoPeticion { TenantId = "tenant-a", Destino = new MemoryStream() }, default));
    }
}

/// <summary>Bucket en memoria: nombre del objeto → contenido y fecha.</summary>
internal sealed class RespaldosEnMemoria : IAlmacenRespaldos
{
    public Dictionary<string, Dictionary<string, (byte[] Contenido, DateTimeOffset TomadoEn)>> Objetos { get; } = new(StringComparer.Ordinal);

    public void Agregar(string bucket, string nombre, DateTimeOffset tomadoEn) => Bucket(bucket)[nombre] = ([1, 2, 3], tomadoEn);

    public async Task GuardarAsync(string nombreBucket, string nombre, Stream contenido, CancellationToken ct)
    {
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia, ct);
        var tomadoEn = DateTimeOffset.ParseExact(nombre["respaldos/silo-".Length..^".dump".Length], "yyyyMMdd'T'HHmmss'Z'",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
        Bucket(nombreBucket).Add(nombre, (copia.ToArray(), tomadoEn));
    }

    public Task<IReadOnlyList<Respaldo>> ListarAsync(string nombreBucket, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Respaldo>>(Objetos.GetValueOrDefault(nombreBucket)?
            .Select(o => new Respaldo(o.Key, o.Value.TomadoEn, o.Value.Contenido.Length))
            .OrderByDescending(r => r.TomadoEn).ToList() ?? []);

    public async Task DescargarAsync(string nombreBucket, string nombre, Stream destino, CancellationToken ct) =>
        await destino.WriteAsync(Objetos[nombreBucket][nombre].Contenido, ct);

    public Task EliminarAsync(string nombreBucket, string nombre, CancellationToken ct)
    {
        Objetos.GetValueOrDefault(nombreBucket)?.Remove(nombre);
        return Task.CompletedTask;
    }

    private Dictionary<string, (byte[], DateTimeOffset)> Bucket(string nombre) =>
        Objetos.TryGetValue(nombre, out var bucket) ? bucket : Objetos[nombre] = new(StringComparer.Ordinal);
}
