namespace FlowRide.Aprovisionamiento;

/// <summary>Comando de la línea de comandos ya interpretado: nombre y opciones <c>--clave valor</c>.</summary>
public sealed record Comando(string Nombre, IReadOnlyDictionary<string, string> Opciones)
{
    public const string AyudaNombre = "ayuda";
    public const string CrearOperador = "crear-operador";
    public const string Migrar = "migrar";
    public const string MigrarTodos = "migrar-todos";
    public const string Listar = "listar";
    public const string RegistrarRuta = "registrar-ruta";
    public const string ListarRutas = "listar-rutas";
    public const string Respaldar = "respaldar";
    public const string ListarRespaldos = "listar-respaldos";
    public const string DescargarRespaldo = "descargar-respaldo";

    public static readonly string Ayuda = """
        Uso: flowride-aprovisionamiento <comando> [opciones]

        Comandos:
          crear-operador --id <tenant> --razon-social <texto> --nit <texto> --correo <correo>
                         [--admin-nombre <texto> --admin-correo <correo> --admin-telefono <whatsapp>]
              Registra el operador y aprovisiona su Silo (base, rol, secretos y migraciones) y su realm de
              Keycloak. Con las opciones --admin-* crea también su primer Administrador, que recibe un correo
              para definir su contraseña. Es idempotente: con el mismo --id se puede volver a ejecutar.
          migrar --id <tenant>      Aplica las migraciones pendientes al Silo de un operador.
          migrar-todos              Migra todos los operadores no suspendidos, en orden por Id.
          listar                    Muestra los operadores del catálogo.
          registrar-ruta --id <tenant> --url <url-del-stack> [--subdominio <texto>] [--phone-number-id <id>]
              Registra o actualiza cómo llega el borde común al stack del operador (ADR-0012): su subdominio
              (por defecto, el Id), la URL de su API y el phone_number_id de su número de WhatsApp en Meta.
          listar-rutas              Muestra las rutas del borde.
          respaldar --id <tenant> --archivo <ruta>
              Guarda en el bucket del operador un respaldo de su Silo ya generado con pg_dump (ADR-0013) y borra
              los de más de 35 días, salvo el más reciente. Lo usa scripts/respaldar.ps1.
          listar-respaldos --id <tenant>    Muestra los respaldos del operador, del más reciente al más antiguo.
          descargar-respaldo --id <tenant> --archivo <ruta> [--nombre <respaldo>]
              Descarga un respaldo del operador (por defecto, el más reciente) para restaurarlo.
          ayuda                     Muestra esta ayuda.
        """;

    private static readonly Dictionary<string, string[]> OpcionesRequeridas = new(StringComparer.Ordinal)
    {
        [CrearOperador] = ["id", "razon-social", "nit", "correo"],
        [Migrar] = ["id"],
        [MigrarTodos] = [],
        [Listar] = [],
        [RegistrarRuta] = ["id", "url"],
        [ListarRutas] = [],
        [Respaldar] = ["id", "archivo"],
        [ListarRespaldos] = ["id"],
        [DescargarRespaldo] = ["id", "archivo"],
        [AyudaNombre] = []
    };

    /// <summary>Opciones que van todas juntas o ninguna.</summary>
    private static readonly Dictionary<string, string[]> OpcionesOpcionales = new(StringComparer.Ordinal)
    {
        [CrearOperador] = ["admin-nombre", "admin-correo", "admin-telefono"]
    };

    /// <summary>Opciones opcionales que van cada una por su lado.</summary>
    private static readonly Dictionary<string, string[]> OpcionesIndependientes = new(StringComparer.Ordinal)
    {
        [RegistrarRuta] = ["subdominio", "phone-number-id"],
        [DescargarRespaldo] = ["nombre"]
    };

    public string Opcion(string nombre) => Opciones[nombre];

    public bool Tiene(string nombre) => Opciones.ContainsKey(nombre);

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

        var opcionales = OpcionesOpcionales.GetValueOrDefault(nombre, []);
        var independientes = OpcionesIndependientes.GetValueOrDefault(nombre, []);
        var desconocidas = opciones.Keys
            .Where(clave => !requeridas.Contains(clave) && !opcionales.Contains(clave) && !independientes.Contains(clave)).ToArray();
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

        var opcionalFaltante = opcionales.FirstOrDefault(clave => !opciones.ContainsKey(clave));
        if (opcionalFaltante is not null && opcionales.Any(opciones.ContainsKey))
        {
            error = $"Falta la opción --{opcionalFaltante}: las opciones --{string.Join(", --", opcionales)} van juntas.";
            return false;
        }

        comando = new Comando(nombre, opciones);
        return true;
    }
}
