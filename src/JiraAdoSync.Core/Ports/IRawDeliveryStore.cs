namespace JiraAdoSync.Core.Ports;

/// <summary>Keeps the raw body of each delivery. The host implements it with Blob Storage (ADR 0004).</summary>
public interface IRawDeliveryStore
{
    /// <summary>
    /// Stores <paramref name="body"/> under <paramref name="blobName"/>. Storing the same name with the same body again is
    /// harmless. Returns false when the write failed, so that the caller can answer the vendor with a retryable status.
    /// </summary>
    Task<bool> TryStoreAsync(string blobName, ReadOnlyMemory<byte> body, CancellationToken cancellationToken);

    /// <summary>Loads the body stored under <paramref name="blobName"/>, or returns null when there is none.</summary>
    Task<byte[]?> LoadAsync(string blobName, CancellationToken cancellationToken);
}
