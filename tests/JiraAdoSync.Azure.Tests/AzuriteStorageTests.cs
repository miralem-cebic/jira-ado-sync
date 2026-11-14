using System.Text;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Azure.Tests;

/// <summary>
/// Runs the three storage adapters against a storage emulator. Set SYNC_TEST_STORAGE to a connection string, for
/// example UseDevelopmentStorage=true while Azurite runs. Without it the tests are skipped. Each test creates its own
/// containers, queue and table, and deletes them afterwards. The clients use the newest service version that Azurite 3.37
/// accepts (Blob and Queue 2025-11-05, Table 2020-12-06). The production clients keep the SDK default.
/// </summary>
public class AzuriteStorageTests : IAsyncLifetime
{
    private const string ConnectionVariable = "SYNC_TEST_STORAGE";
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly string? _connection = Environment.GetEnvironmentVariable(ConnectionVariable);
    private readonly string _suffix = Guid.NewGuid().ToString("N")[..10];
    private BlobContainerClient? _container;
    private QueueClient? _queue;
    private TableClient? _table;

    public async ValueTask InitializeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        _container = new BlobContainerClient(
            _connection,
            "synctest" + _suffix,
            new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05));
        _queue = StorageClients.CreateDeliveryQueue(
            _connection,
            "synctest" + _suffix,
            new QueueClientOptions(QueueClientOptions.ServiceVersion.V2025_11_05));
        _table = new TableClient(
            _connection,
            "SyncTest" + _suffix,
            new TableClientOptions(TableClientOptions.ServiceVersion.V2020_12_06));

        await _container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _table.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        await _container!.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _queue!.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _table!.DeleteAsync(cancellationToken).ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BodyIsStoredAndLoadedUnchanged()
    {
        SkipWithoutStorage();
        var store = new BlobRawDeliveryStore(_container!);
        var body = Encoding.UTF8.GetBytes("{\"webhookEvent\":\"jira:issue_updated\"}");

        Assert.True(await store.TryStoreAsync("jira/one", body, TestContext.Current.CancellationToken));

        var loaded = await store.LoadAsync("jira/one", TestContext.Current.CancellationToken);
        Assert.Equal(body, loaded);
    }

    [Fact]
    public async Task MissingBodyLoadsAsNull()
    {
        SkipWithoutStorage();
        var store = new BlobRawDeliveryStore(_container!);

        Assert.Null(await store.LoadAsync("jira/never-stored", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoringTheSameBodyAgainKeepsOneBlob()
    {
        SkipWithoutStorage();
        var store = new BlobRawDeliveryStore(_container!);
        var body = Encoding.UTF8.GetBytes("{\"webhookEvent\":\"jira:issue_updated\"}");
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await store.TryStoreAsync("jira/same", body, cancellationToken));
        Assert.True(await store.TryStoreAsync("jira/same", body, cancellationToken));

        var names = new List<string>();
        await foreach (var blob in _container!.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            names.Add(blob.Name);
        }

        Assert.Equal(["jira/same"], names);
    }

    [Fact]
    public async Task MessageIsEnqueuedAndReadBack()
    {
        SkipWithoutStorage();
        var dispatcher = new StorageQueueDispatcher(_queue!);
        var message = new DeliveryMessage(Side.Ado, "ado/abc123");
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await dispatcher.TryEnqueueAsync(message, cancellationToken));

        var received = await _queue!.ReceiveMessageAsync(cancellationToken: cancellationToken);
        Assert.Equal(message, DeliveryMessageJson.Deserialize(received.Value.MessageText));
    }

    [Fact]
    public async Task SyncStateIsCreatedOnceAndLaterSavesNeedTheCurrentETag()
    {
        SkipWithoutStorage();
        var store = new TableSyncStateStore(_table!);
        var cancellationToken = TestContext.Current.CancellationToken;
        var state = new SyncState("PROJ-1", "42", "hash-1", T0, Side.Jira);

        Assert.True(await store.TrySaveAsync("proj", state, null, cancellationToken));
        Assert.False(await store.TrySaveAsync("proj", state, null, cancellationToken));

        var stored = await store.FindByJiraKeyAsync("proj", "PROJ-1", cancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(state, stored.State);

        var next = state with { LastSyncedHash = "hash-2", LastChangedAt = T0.AddMinutes(1), LastWriter = Side.Ado };
        Assert.True(await store.TrySaveAsync("proj", next, stored.ETag, cancellationToken));

        // The ETag of the first read is no longer current, so a second writer with it loses.
        var stale = next with { LastSyncedHash = "hash-3" };
        Assert.False(await store.TrySaveAsync("proj", stale, stored.ETag, cancellationToken));
    }

    [Fact]
    public async Task StateIsFoundByAdoWorkItemId()
    {
        SkipWithoutStorage();
        var store = new TableSyncStateStore(_table!);
        var cancellationToken = TestContext.Current.CancellationToken;
        await store.TrySaveAsync("proj", new SyncState("PROJ-2", "77", "hash", T0, Side.Jira), null, cancellationToken);

        var found = await store.FindByAdoIdAsync("proj", "77", cancellationToken);
        var missing = await store.FindByAdoIdAsync("proj", "78", cancellationToken);

        Assert.NotNull(found);
        Assert.Equal("PROJ-2", found.State.JiraKey);
        Assert.Null(missing);
    }

    private void SkipWithoutStorage()
    {
        if (_connection is null)
        {
            Assert.Skip($"Set {ConnectionVariable} to run the storage tests against an emulator.");
        }
    }
}
