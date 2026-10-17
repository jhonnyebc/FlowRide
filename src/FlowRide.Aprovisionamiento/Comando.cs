namespace FlowRide.Aprovisionamiento;

/// <summary>Comando de la línea de comandos ya interpretado: nombre y opciones <c>--clave valor</c>.</summary>
public sealed record Comando(string Nombre, IReadOnlyDictionary<string, string> Opciones)
{
    public const string AyudaNombre = "ayuda";
    public const string CrearOperador = "crear-operador";
    public const string Migrar = "migrar";
    public const string MigrarTodos = "migrar-todos";
    public const string Listar = "listar";

    public static readonly string Ayuda = """
        Uso: flowride-aprovisionamiento <comando> [opciones]

        Comandos:
          crear-operador --id <tenant> --razon-social <texto> --nit <texto> --correo <correo>
              Registra el operador y aprovisiona su Silo: base, rol, secreto y migraciones.
              Es idempotente: con el mismo --id se puede volver a ejecutar.
          migrar --id <tenant>      Aplica las migraciones pendientes al Silo de un operador.
          migrar-todos              Migra todos los operadores no suspendidos, en orden por Id.
          listar                    Muestra los operadores del catálogo.
          ayuda                     Muestra esta ayuda.
        """;

    private static readonly Dictionary<string, string[]> OpcionesRequeridas = new(StringComparer.Ordinal)
    {
        [CrearOperador] = ["id", "razon-social", "nit", "correo"],
        [Migrar] = ["id"],
        [MigrarTodos] = [],
        [Listar] = [],
        [AyudaNombre] = []
    };

    public string Opcion(string nombre) => Opciones[nombre];

    public static bool TryParse(IReadOnlyList<string> args, out Comando comando, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        comando = new Comando(AyudaNombre, new Dictionary<string, string>());
        error = string.Empty;

        if (args.Count == 0)
        {
            error = "Falta el comando.";
            return false;
        }

        var nombre = args[0];
        if (nombre is "-h" or "--help") nombre = AyudaNombre;
        if (!OpcionesRequeridas.TryGetValue(nombre, out var requeridas))
        {
            error = $"Comando desconocido: '{nombre}'.";
            return false;
        }

        var opciones = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2)
        {
            var clave = args[i];
            if (!clave.StartsWith("--", StringComparison.Ordinal) || clave.Length == 2)
            {
                error = $"Se esperaba una opción '--nombre' y llegó '{clave}'.";
                return false;
            }
            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Falta el valor de '{clave}'.";
                return false;
            }
            opciones[clave[2..]] = args[i + 1];
        }

        var desconocidas = opciones.Keys.Where(clave => !requeridas.Contains(clave)).ToArray();
        if (desconocidas.Length > 0)
        {
            error = $"Opción desconocida para '{nombre}': --{desconocidas[0]}.";
            return false;
        }

        var faltante = requeridas.FirstOrDefault(clave => !opciones.ContainsKey(clave));
        if (faltante is not null)
        {
            error = $"Falta la opción --{faltante} para '{nombre}'.";
            return false;
        }

        comando = new Comando(nombre, opciones);
        return true;
    }
}
