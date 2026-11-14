using Azure.Data.Tables;
using Azure.Storage.Blobs;

namespace JiraAdoSync.Azure;

/// <summary>Creates the storage resources of the host if they are missing. Calling it again is harmless.</summary>
public static class StorageResources
{
    /// <param name="connectionString">The storage connection string.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    public static async Task EnsureCreatedAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        await new BlobContainerClient(connectionString, StorageNames.RawDeliveryContainer)
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await StorageClients.CreateDeliveryQueue(connectionString, StorageNames.DeliveryQueue)
            .CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await new TableClient(connectionString, StorageNames.SyncItemsTable)
            .CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }
}
