using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FlowRide.IntegrationTests.Infraestructura;

/// <summary>Proveedor de logs en memoria que conserva el mensaje, el EventId y los valores de los scopes.</summary>
public sealed class RegistroCapturado : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public ConcurrentQueue<EntradaDeLog> Entradas { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    public sealed record EntradaDeLog(
        string Categoria,
        LogLevel Nivel,
        EventId EventId,
        string Mensaje,
        IReadOnlyDictionary<string, object?> Valores);

    private sealed class Logger(RegistroCapturado proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => proveedor._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
            proveedor._scopes.ForEachScope((scope, acumulado) => Agregar(scope, acumulado), valores);
            Agregar(state, valores);

            proveedor.Entradas.Enqueue(new EntradaDeLog(categoria, logLevel, eventId, formatter(state, exception), valores));
        }

        private static void Agregar(object? estado, Dictionary<string, object?> valores)
        {
            if (estado is not IEnumerable<KeyValuePair<string, object>> pares) return;
            foreach (var (clave, valor) in pares) valores[clave] = valor;
        }
    }
}
