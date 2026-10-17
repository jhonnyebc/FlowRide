using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowRide.Infrastructure.Persistencia.Configuraciones;

/// <summary>Piezas de mapeo comunes a las entidades del Silo.</summary>
internal static class Mapeo
{
    /// <summary>Largo máximo de los enums guardados como texto (legibles en consultas y auditoría).</summary>
    public const int LargoEnum = 20;

    public const string NombreVersion = "Version";

    /// <summary>
    /// Token de concurrencia optimista (ADR-0007): la columna de sistema <c>xmin</c> de PostgreSQL.
    /// No aparece en el dominio; vive como propiedad sombra.
    /// </summary>
    public static void ConVersion<T>(this EntityTypeBuilder<T> entidad) where T : class =>
        entidad.Property<uint>(NombreVersion).IsRowVersion();

    public static PropertyBuilder<TEnum> ComoTexto<TEnum>(this PropertyBuilder<TEnum> propiedad) =>
        propiedad.HasConversion<string>().HasMaxLength(LargoEnum);

    /// <summary>Lista SQL de valores de un enum para filtros y restricciones (<c>'A', 'B'</c>).</summary>
    public static string ListaSql<TEnum>(params TEnum[] valores) where TEnum : struct, Enum =>
        string.Join(", ", valores.Select(valor => $"'{valor}'"));
}
