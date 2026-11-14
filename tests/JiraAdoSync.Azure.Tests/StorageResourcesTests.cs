using Azure.Storage.Blobs;

namespace JiraAdoSync.Azure.Tests;

public class StorageResourcesTests
{
    private const string ConnectionVariable = "SYNC_TEST_STORAGE";

    [Fact]
    public async Task ResourcesAreCreatedAndCreatingThemAgainIsHarmless()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (connection is null)
        {
            Assert.Skip($"Set {ConnectionVariable} to run the storage tests against an emulator.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        await StorageResources.EnsureCreatedAsync(connection, cancellationToken);
        await StorageResources.EnsureCreatedAsync(connection, cancellationToken);

        var container = new BlobContainerClient(connection, StorageNames.RawDeliveryContainer);
        var queue = StorageClients.CreateDeliveryQueue(connection, StorageNames.DeliveryQueue);
        Assert.True(await container.ExistsAsync(cancellationToken));
        Assert.True(await queue.ExistsAsync(cancellationToken));
    }
}
