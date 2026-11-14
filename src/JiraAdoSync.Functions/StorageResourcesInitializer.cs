using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace JiraAdoSync.Functions;

/// <summary>Creates the container, the queue and the table at startup if they are missing. Idempotent.</summary>
public sealed class StorageResourcesInitializer(IConfiguration configuration) : IHostedService
{
    private readonly string _storage = configuration[SyncNames.StorageConnectionSetting]
        ?? throw new InvalidOperationException($"The app setting {SyncNames.StorageConnectionSetting} is not set.");

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await new BlobContainerClient(_storage, SyncNames.RawDeliveryContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await new QueueClient(_storage, SyncNames.DeliveryQueue).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await new TableClient(_storage, SyncNames.SyncItemsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
