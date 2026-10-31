using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FlowRide.Application.Tenancy.Salida.Puertos;
using FlowRide.Domain.Plataforma.Respaldos;

namespace FlowRide.Infrastructure.Tenancy;

/// <summary>
/// Respaldos de cada Silo en la carpeta <c>respaldos/</c> del bucket del operador (ADR-0013; Azurite en local). En
/// Azure, el contenedor de respaldos lleva además inmutabilidad y nivel Cool por política de ciclo de vida.
/// </summary>
public sealed class AlmacenRespaldosAzureBlob(string cadenaConexion) : IAlmacenRespaldos
{
    private readonly BlobServiceClient _servicio = new(cadenaConexion);

    public async Task GuardarAsync(string nombreBucket, string nombre, Stream contenido, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var contenedor = _servicio.GetBlobContainerClient(nombreBucket);
        await contenedor.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        // Un respaldo no se sobrescribe nunca: si el nombre ya existe, falla.
        await contenedor.GetBlobClient(nombre).UploadAsync(contenido, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
            Conditions = new BlobRequestConditions { IfNoneMatch = Azure.ETag.All }
        }, ct);
    }

    public async Task<IReadOnlyList<Respaldo>> ListarAsync(string nombreBucket, CancellationToken ct)
    {
        var contenedor = _servicio.GetBlobContainerClient(nombreBucket);
        if (!await contenedor.ExistsAsync(ct))
            return [];

        var respaldos = new List<Respaldo>();
        await foreach (var blob in contenedor.GetBlobsAsync(BlobTraits.None, BlobStates.None, PoliticaRetencionRespaldos.Prefijo, ct))
            respaldos.Add(new Respaldo(blob.Name, blob.Properties.CreatedOn ?? DateTimeOffset.MinValue, blob.Properties.ContentLength ?? 0));
        return respaldos.OrderByDescending(respaldo => respaldo.TomadoEn).ThenByDescending(respaldo => respaldo.Nombre, StringComparer.Ordinal).ToList();
    }

    public async Task DescargarAsync(string nombreBucket, string nombre, Stream destino, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        await _servicio.GetBlobContainerClient(nombreBucket).GetBlobClient(nombre).DownloadToAsync(destino, ct);
    }

    public async Task EliminarAsync(string nombreBucket, string nombre, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        await _servicio.GetBlobContainerClient(nombreBucket).GetBlobClient(nombre).DeleteIfExistsAsync(cancellationToken: ct);
    }
}
