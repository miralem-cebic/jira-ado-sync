using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Core.Tests;

public class SyncEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static ProjectMapping Mapping(
        FeatureFlags? features = null,
        IReadOnlyCollection<string>? ignoreJiraStates = null) => new()
        {
            Id = "test",
            JiraProject = "PROJ",
            AdoProject = "Project",
            AreaPath = "Project\\Team",
            StatusMap = new Dictionary<string, string>
            {
                ["To Do"] = "New",
                ["In Progress"] = "Active",
                ["Done"] = "Closed",
            },
            IgnoreJiraStates = ignoreJiraStates ?? Array.Empty<string>(),
            Features = features ?? new FeatureFlags(),
        };

    private static SyncEvent JiraEvent(
        string title = "Hello",
        string status = "In Progress",
        bool echo = false,
        DateTimeOffset? at = null) =>
        new(Side.Jira, echo, at ?? T0, title, "desc", status, "alice");

    private static SyncEvent AdoEvent(
        string title = "Hello",
        string state = "Active",
        bool echo = false,
        DateTimeOffset? at = null) =>
        new(Side.Ado, echo, at ?? T0, title, "desc", state, "alice");

    private static SyncState StateFrom(SyncDecision.Apply apply, DateTimeOffset changedAt) =>
        new("PROJ-1", "42", apply.Hash, changedAt, Side.Jira);

    [Fact]
    public void ChangeMadeByServiceIdentityIsNeverEchoed()
    {
        var decision = SyncEngine.Decide(JiraEvent(echo: true), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.EchoSuppressed), decision);
    }

    [Fact]
    public void NewJiraItemIsAppliedWithJiraStatus()
    {
        var decision = SyncEngine.Decide(JiraEvent(), state: null, Mapping());

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("Hello", apply.Item.Title);
        Assert.Equal("In Progress", apply.Item.JiraStatus);
    }

    [Fact]
    public void AdoStatusIsTranslatedToJiraStatus()
    {
        var decision = SyncEngine.Decide(AdoEvent(state: "Active"), state: null, Mapping());

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("In Progress", apply.Item.JiraStatus);
    }

    [Fact]
    public void UnmappedJiraStatusIsSkipped()
    {
        var decision = SyncEngine.Decide(JiraEvent(status: "Blocked"), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.UnmappedStatus), decision);
    }

    [Fact]
    public void UnmappedAdoStateIsSkipped()
    {
        var decision = SyncEngine.Decide(AdoEvent(state: "Removed"), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.UnmappedStatus), decision);
    }

    [Fact]
    public void SameContentAsLastSyncIsNotWrittenAgain()
    {
        var first = Assert.IsType<SyncDecision.Apply>(
            SyncEngine.Decide(JiraEvent(), state: null, Mapping()));

        var decision = SyncEngine.Decide(
            JiraEvent(at: T0.AddMinutes(5)),
            StateFrom(first, T0),
            Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), decision);
    }

    [Fact]
    public void RoundTripFromAdoWithSameContentIsNoChange()
    {
        // Jira change is applied to ADO. The ADO side then reports the same content back.
        var fromJira = Assert.IsType<SyncDecision.Apply>(
            SyncEngine.Decide(JiraEvent(title: "X", status: "In Progress"), state: null, Mapping()));

        var decision = SyncEngine.Decide(
            AdoEvent(title: "X", state: "Active", at: T0.AddSeconds(3)),
            StateFrom(fromJira, T0),
            Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), decision);
    }

    [Fact]
    public void StaleEventIsSkippedUnderLastWriteWins()
    {
        var state = new SyncState("PROJ-1", "42", "other-hash", T0.AddHours(1), Side.Ado);

        var decision = SyncEngine.Decide(JiraEvent(title: "Older", at: T0), state, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.StaleEvent), decision);
    }

    [Fact]
    public void NewerEventIsAppliedOverExistingState()
    {
        var state = new SyncState("PROJ-1", "42", "other-hash", T0, Side.Ado);

        var decision = SyncEngine.Decide(
            JiraEvent(title: "Newer", at: T0.AddHours(1)),
            state,
            Mapping());

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("Newer", apply.Item.Title);
    }

    [Fact]
    public void DisabledTitleFeatureIgnoresTitleChanges()
    {
        var mapping = Mapping(new FeatureFlags(Title: false));
        var first = Assert.IsType<SyncDecision.Apply>(
            SyncEngine.Decide(JiraEvent(title: "A"), state: null, mapping));

        var decision = SyncEngine.Decide(
            JiraEvent(title: "B", at: T0.AddMinutes(1)),
            StateFrom(first, T0),
            mapping);

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), decision);
    }

    [Fact]
    public void MappingWithTwoJiraStatusesOnOneAdoStateIsInvalid()
    {
        var mapping = new ProjectMapping
        {
            Id = "broken",
            JiraProject = "PROJ",
            AdoProject = "Project",
            AreaPath = "Project",
            StatusMap = new Dictionary<string, string>
            {
                ["To Do"] = "New",
                ["Backlog"] = "New",
            },
        };

        var errors = mapping.Validate();

        Assert.Single(errors);
        Assert.Contains("one-to-one", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void ValidMappingHasNoErrors()
    {
        Assert.Empty(Mapping().Validate());
    }

    [Fact]
    public void IgnoredJiraStatusLetsTitleFlowButDropsStatus()
    {
        // The Jira issue sits in an ignored state (for example On Hold). A title edit should
        // still reach ADO, but the canonical item carries no status so no transition is written.
        var mapping = Mapping(ignoreJiraStates: ["On Hold"]);

        var decision = SyncEngine.Decide(
            JiraEvent(title: "edited", status: "On Hold"),
            state: null,
            mapping);

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("edited", apply.Item.Title);
        Assert.Null(apply.Item.JiraStatus);
    }

    [Fact]
    public void IgnoredJiraStatusWithNoOtherChangeIsNoChange()
    {
        var mapping = Mapping(ignoreJiraStates: ["On Hold"]);
        var first = Assert.IsType<SyncDecision.Apply>(
            SyncEngine.Decide(JiraEvent(title: "same", status: "On Hold"), state: null, mapping));

        var decision = SyncEngine.Decide(
            JiraEvent(title: "same", status: "On Hold", at: T0.AddMinutes(1)),
            StateFrom(first, T0),
            mapping);

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), decision);
    }

    [Fact]
    public void TransitionIntoIgnoredStateDoesNotBlockTitleChange()
    {
        // The item was synced while the Jira status was mapped. It now moves into an ignored
        // state with a title edit. The engine must apply the title change without a status.
        var mapping = Mapping(ignoreJiraStates: ["On Hold"]);
        var mapped = Assert.IsType<SyncDecision.Apply>(
            SyncEngine.Decide(JiraEvent(title: "before", status: "In Progress"), state: null, mapping));

        var decision = SyncEngine.Decide(
            JiraEvent(title: "after", status: "On Hold", at: T0.AddMinutes(1)),
            StateFrom(mapped, T0),
            mapping);

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("after", apply.Item.Title);
        Assert.Null(apply.Item.JiraStatus);
    }

    [Fact]
    public void EchoFromAnIgnoredStateIsStillSuppressed()
    {
        // Loop prevention must not be weakened by the ignore list: the service identity's own
        // write that lands back during an ignored interval still ends as EchoSuppressed.
        var mapping = Mapping(ignoreJiraStates: ["On Hold"]);

        var decision = SyncEngine.Decide(
            JiraEvent(status: "On Hold", echo: true),
            state: null,
            mapping);

        Assert.Equal(new SyncDecision.Skip(SkipReason.EchoSuppressed), decision);
    }

    [Fact]
    public void MappingWithJiraStatusInBothMapAndIgnoreListIsInvalid()
    {
        var mapping = new ProjectMapping
        {
            Id = "broken",
            JiraProject = "PROJ",
            AdoProject = "Project",
            AreaPath = "Project",
            StatusMap = new Dictionary<string, string>
            {
                ["To Do"] = "New",
            },
            IgnoreJiraStates = ["To Do"],
        };

        var errors = mapping.Validate();

        Assert.Single(errors);
        Assert.Contains("both mapped and ignored", errors[0], StringComparison.Ordinal);
    }
}
