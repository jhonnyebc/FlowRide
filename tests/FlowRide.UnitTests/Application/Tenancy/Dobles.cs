using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma;
using FlowRide.Domain.Plataforma.Repositorios;

namespace FlowRide.UnitTests.Application.Tenancy;

/// <summary>Dobles en memoria de los puertos de salida de Tenancy; registran lo que se les pidió.</summary>
internal sealed class OperadoresEnMemoria : IOperadorRepositorio
{
    public Dictionary<string, Operador> Operadores { get; } = new(StringComparer.Ordinal);
    public int Creados { get; private set; }
    public int Modificados { get; private set; }

    public Task CrearAsync(Operador entidad, CancellationToken ct)
    {
        Operadores.Add(entidad.Id, entidad);
        Creados++;
        return Task.CompletedTask;
    }

    public Task ModificarAsync(string id, Operador entidad, CancellationToken ct)
    {
        Operadores[id] = entidad;
        Modificados++;
        return Task.CompletedTask;
    }

    public Task<Operador?> ConsultarPorIdAsync(string id, CancellationToken ct) =>
        Task.FromResult(Operadores.GetValueOrDefault(id));

    public Task<IReadOnlyList<Operador>> ConsultarTodosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Operador>>(Operadores.Values.ToList());

    public Task<Operador?> ConsultarPorNitAsync(string nit, CancellationToken ct) =>
        Task.FromResult(Operadores.Values.FirstOrDefault(operador => operador.Nit == nit));

    public Operador Registrar(string id, EstadoOperador estado = EstadoOperador.Activo, string? nit = null)
    {
        var operador = new Operador(id, $"Operador {id}", nit ?? $"nit-{id}", $"{id}@ops.co",
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        if (estado != EstadoOperador.Aprovisionando) operador.Activar();
        if (estado == EstadoOperador.Suspendido) operador.Suspender();
        Operadores[id] = operador;
        return operador;
    }
}

internal sealed class ServidorEnMemoria : IServidorSilos
{
    public Dictionary<string, string> Silos { get; } = new(StringComparer.Ordinal);

    public Task AsegurarSiloAsync(string nombreSilo, string contrasena, CancellationToken ct)
    {
        Silos[nombreSilo] = contrasena;
        return Task.CompletedTask;
    }

    public string CadenaConexion(string nombreSilo, string contrasena) => $"Database={nombreSilo};Password={contrasena}";
}

internal sealed class MigradorEnMemoria : IMigradorSilo
{
    private readonly HashSet<string> _migradas = new(StringComparer.Ordinal);

    public string[] Migraciones { get; set; } = ["20261017_Inicial"];
    public string? FallaPara { get; set; }

    public Task<IReadOnlyList<string>> MigrarAsync(string cadenaConexion, CancellationToken ct)
    {
        if (FallaPara is not null && cadenaConexion.Contains(FallaPara, StringComparison.Ordinal))
            throw new InvalidOperationException("La base no responde.");

        IReadOnlyList<string> aplicadas = _migradas.Add(cadenaConexion) ? Migraciones : [];
        return Task.FromResult(aplicadas);
    }
}

internal sealed class SecretosEnMemoria : IAlmacenSecretos
{
    public Dictionary<(string Ambito, string Clave), string> Valores { get; } = [];

    public Task<string?> ObtenerAsync(string ambito, string clave, CancellationToken ct) =>
        Task.FromResult(Valores.GetValueOrDefault((ambito, clave)));

    public Task GuardarAsync(string ambito, string clave, string valor, CancellationToken ct)
    {
        Valores[(ambito, clave)] = valor;
        return Task.CompletedTask;
    }

    public void CadenaPara(Operador operador) =>
        Valores[(operador.AmbitoSecretos, ClavesSecretos.CadenaConexionSilo)] = $"Database={operador.NombreSilo}";
}

internal sealed class GeneradorSecuencial : IGeneradorSecretos
{
    private int _siguiente;

    public string NuevaContrasena() => $"clave-{++_siguiente}";
}

internal sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}

internal sealed class IdentidadEnMemoria : IServidorIdentidad
{
    public Dictionary<string, (string TenantId, string Secreto)> Realms { get; } = new(StringComparer.Ordinal);
    public bool Falla { get; set; }

    public Task AsegurarRealmAsync(string nombreRealm, string tenantId, string secretoClienteServicio, CancellationToken ct)
    {
        if (Falla)
            throw new InvalidOperationException("Keycloak no responde.");
        Realms[nombreRealm] = (tenantId, secretoClienteServicio);
        return Task.CompletedTask;
    }
}

internal sealed class ObjetosEnMemoria : IServidorObjetos
{
    public HashSet<string> Buckets { get; } = new(StringComparer.Ordinal);

    public Task AsegurarBucketAsync(string nombreBucket, CancellationToken ct)
    {
        Buckets.Add(nombreBucket);
        return Task.CompletedTask;
    }

    public string CadenaConexion(string nombreBucket) => $"Bucket={nombreBucket}";
}
