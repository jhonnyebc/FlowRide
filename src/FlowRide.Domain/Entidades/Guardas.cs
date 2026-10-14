namespace FlowRide.Domain.Entidades;

/// <summary>Validaciones comunes de las entidades del dominio.</summary>
internal static class Guardas
{
    public static Guid Id(Guid id, string mensaje, string parametro) =>
        id == Guid.Empty ? throw new ArgumentException(mensaje, parametro) : id;

    public static string Texto(string? valor, string parametro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, parametro);
        return valor.Trim();
    }

    public static string? TextoOpcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    public static InvalidOperationException TransicionInvalida(string accion, string entidad, Enum estado) =>
        new($"No se puede {accion} {entidad} en estado {estado}.");
}
