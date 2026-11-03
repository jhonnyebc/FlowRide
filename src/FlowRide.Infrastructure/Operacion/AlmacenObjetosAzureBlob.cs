using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using FlowRide.Application.Operacion.Salida.Puertos;
using FlowRide.Application.Tenancy.Salida.Puertos;

namespace FlowRide.Infrastructure.Operacion;

/// <summary>
/// Bucket del operador en Azure Blob Storage (ADR-0013; Azurite en local). El contenedor es privado y las descargas
/// son URLs firmadas de solo lectura que vencen en ≤ 15 min.
/// </summary>
public sealed class AlmacenObjetosAzureBlob(BlobContainerClient contenedor) : IAlmacenObjetos
{
    public static readonly TimeSpan VigenciaMaxima = TimeSpan.FromMinutes(15);

    public async Task GuardarAsync(string ruta, Stream contenido, string tipoContenido, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        await contenedor.GetBlobClient(ruta).UploadAsync(contenido,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = tipoContenido } }, ct);
    }

    public Task<Uri> UrlDescargaAsync(string ruta, TimeSpan vigencia, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        if (vigencia <= TimeSpan.Zero || vigencia > VigenciaMaxima)
            throw new ArgumentOutOfRangeException(nameof(vigencia), "La URL firmada vence en máximo 15 minutos (ADR-0013).");

        var blob = contenedor.GetBlobClient(ruta);
        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("La credencial del bucket no permite firmar URLs.");
        return Task.FromResult(blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(vigencia)));
    }

    public async Task EliminarAsync(string ruta, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);
        await contenedor.GetBlobClient(ruta).DeleteIfExistsAsync(cancellationToken: ct);
    }
}

/// <summary>
/// Crea el bucket privado de cada operador (herramienta de aprovisionamiento). En local (Azurite) todos los buckets
/// comparten la cuenta de desarrollo; en Azure, la cadena del operador debe limitarse a su contenedor (Fase 12).
/// </summary>
public sealed class ServidorObjetosAzureBlob(string cadenaConexion) : IServidorObjetos
{
    private readonly BlobServiceClient _servicio = new(cadenaConexion);

    public async Task AsegurarBucketAsync(string nombreBucket, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreBucket);
        await _servicio.GetBlobContainerClient(nombreBucket).CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
    }

    public string CadenaConexion(string nombreBucket) => cadenaConexion;
}
