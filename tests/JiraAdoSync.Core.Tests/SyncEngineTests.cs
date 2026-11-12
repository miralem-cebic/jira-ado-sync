using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;
using Xunit;

namespace JiraAdoSync.Core.Tests;

public class SyncEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private static ProjectMapping Mapping(FeatureFlags? features = null) => new()
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
    public void Change_made_by_service_identity_is_never_echoed()
    {
        var decision = SyncEngine.Decide(JiraEvent(echo: true), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.EchoSuppressed), decision);
    }

    [Fact]
    public void New_jira_item_is_applied_with_jira_status()
    {
        var decision = SyncEngine.Decide(JiraEvent(), state: null, Mapping());

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("Hello", apply.Item.Title);
        Assert.Equal("In Progress", apply.Item.JiraStatus);
    }

    [Fact]
    public void Ado_status_is_translated_to_jira_status()
    {
        var decision = SyncEngine.Decide(AdoEvent(state: "Active"), state: null, Mapping());

        var apply = Assert.IsType<SyncDecision.Apply>(decision);
        Assert.Equal("In Progress", apply.Item.JiraStatus);
    }

    [Fact]
    public void Unmapped_jira_status_is_skipped()
    {
        var decision = SyncEngine.Decide(JiraEvent(status: "Blocked"), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.UnmappedStatus), decision);
    }

    [Fact]
    public void Unmapped_ado_state_is_skipped()
    {
        var decision = SyncEngine.Decide(AdoEvent(state: "Removed"), state: null, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.UnmappedStatus), decision);
    }

    [Fact]
    public void Same_content_as_last_sync_is_not_written_again()
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
    public void Round_trip_from_ado_with_same_content_is_no_change()
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
    public void Stale_event_is_skipped_under_last_write_wins()
    {
        var state = new SyncState("PROJ-1", "42", "other-hash", T0.AddHours(1), Side.Ado);

        var decision = SyncEngine.Decide(JiraEvent(title: "Older", at: T0), state, Mapping());

        Assert.Equal(new SyncDecision.Skip(SkipReason.StaleEvent), decision);
    }

    [Fact]
    public void Newer_event_is_applied_over_existing_state()
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
    public void Disabled_title_feature_ignores_title_changes()
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
    public void Mapping_with_two_jira_statuses_on_one_ado_state_is_invalid()
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
        Assert.Contains("one-to-one", errors[0]);
    }

    [Fact]
    public void Valid_mapping_has_no_errors()
    {
        Assert.Empty(Mapping().Validate());
    }
}
