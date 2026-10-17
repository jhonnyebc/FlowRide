namespace FlowRide.Application.Operacion.Salida.Puertos;

/// <summary>
/// Bucket del operador en el almacenamiento de objetos (ADR-0013): nunca público, credencial exclusiva del stack.
/// Las rutas llevan el prefijo de su política de ciclo de vida (p. ej. <c>documents/</c>).
/// </summary>
public interface IAlmacenObjetos
{
    Task GuardarAsync(string ruta, Stream contenido, string tipoContenido, CancellationToken ct);

    /// <summary>URL firmada de solo lectura que vence en <paramref name="vigencia"/> (≤ 15 min).</summary>
    Task<Uri> UrlDescargaAsync(string ruta, TimeSpan vigencia, CancellationToken ct);

    /// <summary>Borra el objeto si existe.</summary>
    Task EliminarAsync(string ruta, CancellationToken ct);
}
