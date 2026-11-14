using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Core.Tests;

public class SyncProcessorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static ProjectMapping Mapping() => new()
    {
        Id = "proj",
        JiraProject = "PROJ",
        AdoProject = "Project",
        AreaPath = "Project\\Team",
        StatusMap = new Dictionary<string, string>
        {
            ["To Do"] = "New",
            ["In Progress"] = "Active",
            ["Done"] = "Closed",
        },
    };

    private static SyncState Linked(DateTimeOffset changedAt) =>
        new("PROJ-1", "42", "hash-before", changedAt, Side.Ado);

    private static SyncEvent JiraEvent(
        string title = "Changed",
        string status = "In Progress",
        bool echo = false,
        DateTimeOffset? at = null) =>
        new(Side.Jira, echo, at ?? T0.AddMinutes(1), title, "desc", status, null);

    private static SyncEvent AdoEvent(string title = "From ADO", string state = "Closed") =>
        new(Side.Ado, false, T0.AddMinutes(1), title, "desc", state, null);

    [Fact]
    public async Task JiraChangeIsWrittenToLinkedAdoItemAndStateIsStored()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        var jira = new FakeJiraClient();
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(jira, ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-1", JiraEvent(title: "Changed")),
            Mapping(),
            CancellationToken.None);

        Assert.IsType<SyncOutcome.Written>(outcome);
        var write = Assert.Single(ado.Applied);
        Assert.Equal("42", write.WorkItemId);
        Assert.Equal("Changed", write.Item.Title);
        Assert.Equal("In Progress", write.Item.JiraStatus);
        Assert.Empty(jira.Applied);

        var saved = await store.FindByJiraKeyAsync("proj", "PROJ-1", CancellationToken.None);
        Assert.Equal(T0.AddMinutes(1), saved!.State.LastChangedAt);
        Assert.Equal(Side.Jira, saved.State.LastWriter);
    }

    [Fact]
    public async Task AdoChangeIsWrittenToLinkedJiraIssue()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        var jira = new FakeJiraClient();
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(jira, ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Ado, "42", AdoEvent(title: "From ADO", state: "Closed")),
            Mapping(),
            CancellationToken.None);

        Assert.IsType<SyncOutcome.Written>(outcome);
        var write = Assert.Single(jira.Applied);
        Assert.Equal("PROJ-1", write.IssueKey);
        Assert.Equal("From ADO", write.Item.Title);
        Assert.Equal("Done", write.Item.JiraStatus);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task SameDeliveryTwiceWritesOnlyOnce()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(new FakeJiraClient(), ado, store);
        var change = new SourceChange(Side.Jira, "PROJ-1", JiraEvent());

        var first = await processor.ProcessAsync(change, Mapping(), CancellationToken.None);
        var second = await processor.ProcessAsync(change, Mapping(), CancellationToken.None);

        Assert.IsType<SyncOutcome.Written>(first);
        Assert.Equal(new SyncOutcome.Skipped(SkipReason.NoChange), second);
        Assert.Single(ado.Applied);
    }

    [Fact]
    public async Task EchoOfTheSyncsOwnWriteIsNotWritten()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(new FakeJiraClient(), ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-1", JiraEvent(echo: true)),
            Mapping(),
            CancellationToken.None);

        Assert.Equal(new SyncOutcome.Skipped(SkipReason.EchoSuppressed), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task OlderChangeDoesNotOverwriteNewerChange()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0.AddHours(1)));
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(new FakeJiraClient(), ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-1", JiraEvent(at: T0)),
            Mapping(),
            CancellationToken.None);

        Assert.Equal(new SyncOutcome.Skipped(SkipReason.StaleEvent), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task ChangeToUnlinkedIssueIsNotWritten()
    {
        var store = new InMemorySyncStateStore();
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(new FakeJiraClient(), ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-9", JiraEvent()),
            Mapping(),
            CancellationToken.None);

        Assert.Equal(new SyncOutcome.Skipped(SkipReason.NotLinked), outcome);
        Assert.Empty(ado.Applied);
    }

    [Fact]
    public async Task FailedTargetWriteThrowsAndLeavesStateUnchanged()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        var processor = new SyncProcessor(new FakeJiraClient(), new FakeAdoClient { FailWith = new HttpRequestException("503") }, store);

        // The queue retries a thrown exception. Nothing is saved, so the retry decides again from the old state.
        await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-1", JiraEvent()),
            Mapping(),
            CancellationToken.None));

        var saved = await store.FindByJiraKeyAsync("proj", "PROJ-1", CancellationToken.None);
        Assert.Equal("hash-before", saved!.State.LastSyncedHash);
    }

    [Fact]
    public async Task ConcurrentStateChangeIsReportedAsConflict()
    {
        var store = new InMemorySyncStateStore();
        store.Seed("proj", Linked(T0));
        store.ConcurrentWriteBeforeNextSave = true;
        var ado = new FakeAdoClient();
        var processor = new SyncProcessor(new FakeJiraClient(), ado, store);

        var outcome = await processor.ProcessAsync(
            new SourceChange(Side.Jira, "PROJ-1", JiraEvent()),
            Mapping(),
            CancellationToken.None);

        Assert.Equal(new SyncOutcome.Conflict(), outcome);
        // The target is written before the state is saved (DESIGN.md, section 5). The retry writes the same values.
        Assert.Single(ado.Applied);
    }
}
