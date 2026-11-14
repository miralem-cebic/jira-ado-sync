using Azure;
using Azure.Storage.Blobs;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Azure;

/// <summary>Keeps raw deliveries in a blob container (ADR 0004). Storing the same name with the same bytes again is harmless.</summary>
public sealed class BlobRawDeliveryStore(BlobContainerClient container) : IRawDeliveryStore
{
    private readonly BlobContainerClient _container = container ?? throw new ArgumentNullException(nameof(container));

    /// <inheritdoc />
    public async Task<bool> TryStoreAsync(string blobName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(blobName);

        try
        {
            await _container.GetBlobClient(blobName)
                .UploadAsync(new BinaryData(body), overwrite: true, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> LoadAsync(string blobName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(blobName);

        try
        {
            var response = await _container.GetBlobClient(blobName).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
