using System.Text;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Core.Tests;

public class DeliveryProcessorTests
{
    private static readonly DateTimeOffset LastSynced = new(2025, 12, 31, 9, 0, 0, TimeSpan.Zero);

    private const string JiraUpdate =
        "{\"webhookEvent\":\"jira:issue_updated\",\"timestamp\":1767261600000,\"user\":{\"accountId\":\"someone-else\"}," +
        "\"issue\":{\"key\":\"PROJ-1\",\"fields\":{\"summary\":\"New title\",\"status\":{\"name\":\"In Progress\"}," +
        "\"project\":{\"key\":\"PROJ\"}}}}";

    private const string AdoUpdate =
        "{\"eventType\":\"workitem.updated\",\"resource\":{\"workItemId\":42,\"rev\":8," +
        "\"revisedBy\":{\"uniqueName\":\"someone@example.test\"},\"revision\":{\"fields\":{" +
        "\"System.TeamProject\":\"Project\",\"System.ChangedDate\":\"2026-01-01T10:05:00Z\"," +
        "\"System.Title\":\"From ADO\",\"System.State\":\"Closed\"}}}}";

    private const string Comment = "{\"webhookEvent\":\"comment_created\",\"comment\":{\"id\":\"1\"}}";

    private static readonly SyncIdentities Identities = new("service-account-id", "sync@example.test");

    private static ProjectMapping Mapping() => new()
    {
        Id = "proj",
        JiraProject = "PROJ",
        AdoProject = "Project",
        AreaPath = "Project\\Team",
        StatusMap = new Dictionary<string, string>
        {
            ["In Progress"] = "Active",
            ["Done"] = "Closed",
        },
    };

    private static (DeliveryProcessor Processor, InMemoryRawDeliveryStore Store, FakeJiraClient Jira, FakeAdoClient Ado, InMemorySyncStateStore State)
        Create(FakeAdoClient? ado = null)
    {
        var store = new InMemoryRawDeliveryStore();
        var state = new InMemorySyncStateStore();
        state.Seed("proj", new SyncState("PROJ-1", "42", "hash-before", LastSynced, Side.Ado));
        var jira = new FakeJiraClient();
        var adoClient = ado ?? new FakeAdoClient();
        var sync = new SyncProcessor(jira, adoClient, state);
        var processor = new DeliveryProcessor(store, sync, Identities, [Mapping()]);
        return (processor, store, jira, adoClient, state);
    }

    /// <summary>Stores the body as the receiver would, and returns the message that the queue would carry.</summary>
    private static DeliveryMessage Stored(InMemoryRawDeliveryStore store, Side source, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var blobName = DeliveryKey.For(source, bytes);
        store.Seed(blobName, bytes);
        return new DeliveryMessage(source, blobName);
    }

    [Fact]
    public async Task JiraDeliveryIsMappedAndWrittenToTheLinkedAdoItem()
    {
        var (processor, store, _, ado, _) = Create();

        var outcome = await processor.ProcessAsync(
            Stored(store, Side.Jira, JiraUpdate),
            CancellationToken.None);

        var processed = Assert.IsType<DeliveryOutcome.Processed>(outcome);
        Assert.IsType<SyncOutcome.Written>(processed.Outcome);
        var write = Assert.Single(ado.Applied);
        Assert.Equal("42", write.WorkItemId);
        Assert.Equal("New title", write.Item.Title);
        Assert.Equal("In Progress", write.Item.JiraStatus);
    }

    [Fact]
    public async Task AdoDeliveryIsMappedByProjectNameAndWrittenToTheLinkedJiraIssue()
    {
        var (processor, store, jira, _, _) = Create();

        var outcome = await processor.ProcessAsync(
            Stored(store, Side.Ado, AdoUpdate),
            CancellationToken.None);

        var processed = Assert.IsType<DeliveryOutcome.Processed>(outcome);
        Assert.IsType<SyncOutcome.Written>(processed.Outcome);
        var write = Assert.Single(jira.Applied);
        Assert.Equal("PROJ-1", write.IssueKey);
        Assert.Equal("From ADO", write.Item.Title);
        Assert.Equal("Done", write.Item.JiraStatus);
    }

    [Fact]
    public async Task EchoOfTheSyncsOwnWriteIsProcessedAsSkippedAndWritesNothing()
    {
        var (processor, store, jira, ado, _) = Create();
        var echo = JiraUpdate.Replace("someone-else", "service-account-id", StringComparison.Ordinal);

        var outcome = await processor.ProcessAsync(Stored(store, Side.Jira, echo), CancellationToken.None);

        var processed = Assert.IsType<DeliveryOutcome.Processed>(outcome);
        Assert.Equal(new SyncOutcome.Skipped(SkipReason.EchoSuppressed), processed.Outcome);
        Assert.Empty(ado.Applied);
        Assert.Empty(jira.Applied);
    }

    [Fact]
    public async Task MissingBodyIsDroppedAndNotRetried()
    {
        var (processor, _, _, ado, _) = Create();

        var outcome = await processor.ProcessAsync(
            new DeliveryMessage(Side.Jira, "jira/not-stored"),
            CancellationToken.None);

        Assert.Equal(new DeliveryOutcome.Dropped(DropReason.BodyMissing), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task BodyThatIsNotJsonIsDroppedAndNotRetried()
    {
        var (processor, store, _, ado, _) = Create();

        var outcome = await processor.ProcessAsync(Stored(store, Side.Jira, "not json"), CancellationToken.None);

        Assert.Equal(new DeliveryOutcome.Dropped(DropReason.NotJson), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task EventTypeTheSyncDoesNotHandleIsDropped()
    {
        var (processor, store, _, ado, _) = Create();

        var outcome = await processor.ProcessAsync(Stored(store, Side.Jira, Comment), CancellationToken.None);

        Assert.Equal(new DeliveryOutcome.Dropped(DropReason.UnsupportedEvent), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task ChangeInAProjectWithoutMappingIsDropped()
    {
        var (processor, store, _, ado, _) = Create();
        var otherProject = JiraUpdate.Replace("\"key\":\"PROJ\"}", "\"key\":\"OTHER\"}", StringComparison.Ordinal);

        var outcome = await processor.ProcessAsync(Stored(store, Side.Jira, otherProject), CancellationToken.None);

        Assert.Equal(new DeliveryOutcome.Dropped(DropReason.UnmappedProject), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task LostSaveRaceAsksForARetry()
    {
        var (processor, store, _, _, state) = Create();
        state.ConcurrentWriteBeforeNextSave = true;

        var outcome = await processor.ProcessAsync(Stored(store, Side.Jira, JiraUpdate), CancellationToken.None);

        Assert.IsType<DeliveryOutcome.RetryLater>(outcome);
    }

    [Fact]
    public async Task FailedTargetWriteIsNotCaughtSoTheQueueRetriesIt()
    {
        var failing = new FakeAdoClient { FailWith = new HttpRequestException("503") };
        var (processor, store, _, _, _) = Create(failing);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            processor.ProcessAsync(Stored(store, Side.Jira, JiraUpdate), CancellationToken.None));
    }
}
