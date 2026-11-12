using System.Text.Json;
using JiraAdoSync.Core.Ado;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Jira;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;
using Xunit;

namespace JiraAdoSync.Core.Tests;

/// <summary>
/// End-to-end checks across both systems: a change made in Jira is written to ADO, and the ADO side
/// reporting the same content back must not trigger another write. This is the loop-prevention guarantee.
/// </summary>
public class RoundTripTests
{
    private const string JiraUpdated = """
    {
      "webhookEvent": "jira:issue_updated",
      "timestamp": 1767261600000,
      "user": { "accountId": "acc-123", "displayName": "Someone" },
      "issue": {
        "key": "PROJ-7",
        "fields": {
          "summary": "Login bug",
          "description": {
            "type": "doc",
            "version": 1,
            "content": [
              { "type": "paragraph", "content": [ { "type": "text", "text": "Line one" } ] },
              { "type": "paragraph", "content": [ { "type": "text", "text": "Line two" } ] }
            ]
          },
          "status": { "name": "In Progress" },
          "project": { "key": "PROJ" }
        }
      }
    }
    """;

    private static readonly ProjectMapping Mapping = new()
    {
        Id = "PROJ",
        JiraProject = "PROJ",
        AdoProject = "Project",
        AreaPath = "Project",
        StatusMap = new Dictionary<string, string>
        {
            ["To Do"] = "New",
            ["In Progress"] = "Active",
            ["Done"] = "Closed",
        },
    };

    private static string AdoEcho(string title, string description, string state, string changedBy) => $$"""
    {
      "eventType": "workitem.updated",
      "resource": {
        "workItemId": 42,
        "rev": 6,
        "revisedBy": { "uniqueName": "{{changedBy}}" },
        "fields": {},
        "revision": {
          "id": 42,
          "rev": 6,
          "fields": {
            "System.TeamProject": "Project",
            "System.Title": "{{title}}",
            "System.Description": "{{description}}",
            "System.State": "{{state}}",
            "System.ChangedDate": "2026-01-01T10:05:00Z"
          }
        }
      }
    }
    """;

    [Fact]
    public void Jira_change_is_written_and_its_echo_from_ado_is_not_written_back()
    {
        var jira = JiraWebhookParser.Parse(JiraUpdated, "svc-account")!;
        var decision = Assert.IsType<SyncDecision.Apply>(SyncEngine.Decide(jira.Event, null, Mapping));

        // What we would send to ADO.
        var patch = JsonDocument.Parse(AdoPatchBuilder.Build(5, decision.Item, Mapping)).RootElement;
        Assert.Equal("test", patch[0].GetProperty("op").GetString());
        Assert.Equal(5, patch[0].GetProperty("value").GetInt32());
        Assert.Equal("Active", patch[3].GetProperty("value").GetString());

        // ADO stores the description as HTML and reports it back. The plain text must still match.
        var state = new SyncState("PROJ-7", "42", decision.Hash, jira.Event.ChangedAt, Side.Jira);
        var echo = AdoWebhookParser.Parse(
            AdoEcho("Login bug", "Line one<br>Line two", "Active", "someone@example.com"),
            "svc-ado")!;

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), SyncEngine.Decide(echo.Event, state, Mapping));
    }

    [Fact]
    public void Ado_echo_with_different_html_wrapping_is_still_no_change()
    {
        var jira = JiraWebhookParser.Parse(JiraUpdated, "svc-account")!;
        var decision = Assert.IsType<SyncDecision.Apply>(SyncEngine.Decide(jira.Event, null, Mapping));
        var state = new SyncState("PROJ-7", "42", decision.Hash, jira.Event.ChangedAt, Side.Jira);

        // ADO may wrap each line in a div instead of using <br>.
        var echo = AdoWebhookParser.Parse(
            AdoEcho("Login bug", "<div>Line one</div><div>Line two</div>", "Active", "someone@example.com"),
            "svc-ado")!;

        Assert.Equal(new SyncDecision.Skip(SkipReason.NoChange), SyncEngine.Decide(echo.Event, state, Mapping));
    }

    [Fact]
    public void Ado_echo_with_a_real_change_is_applied()
    {
        var jira = JiraWebhookParser.Parse(JiraUpdated, "svc-account")!;
        var decision = Assert.IsType<SyncDecision.Apply>(SyncEngine.Decide(jira.Event, null, Mapping));
        var state = new SyncState("PROJ-7", "42", decision.Hash, jira.Event.ChangedAt, Side.Jira);

        // Someone changed the title in ADO, a minute later.
        var echo = AdoWebhookParser.Parse(
            AdoEcho("Login bug (ADO)", "Line one<br>Line two", "Active", "someone@example.com"),
            "svc-ado")!;

        var applied = Assert.IsType<SyncDecision.Apply>(SyncEngine.Decide(echo.Event, state, Mapping));
        Assert.Equal("Login bug (ADO)", applied.Item.Title);
    }

    [Theory]
    [InlineData("a < b & \"c\"\nline 2")]
    [InlineData("first\n\nthird")]
    [InlineData("ümlauts and emoji 🚀")]
    public void Html_round_trip_preserves_plain_text(string text)
    {
        var html = AdoHtml.FromPlainText(text);

        Assert.Equal(text, AdoHtml.ToPlainText(html));
    }

    [Fact]
    public void Jira_request_body_uses_adf_for_description()
    {
        var item = new CanonicalItem("Title", "Line one\nLine two", "In Progress", null);

        var body = JsonDocument.Parse(JiraRequests.BuildIssueUpdate(item)).RootElement;

        var description = body.GetProperty("fields").GetProperty("description");
        Assert.Equal("doc", description.GetProperty("type").GetString());
        Assert.Equal(2, description.GetProperty("content").GetArrayLength());
        Assert.False(body.GetProperty("fields").TryGetProperty("assignee", out _));
    }

    [Fact]
    public void Transition_is_found_by_target_status_name()
    {
        const string transitions = """
        {
          "transitions": [
            { "id": "11", "name": "Start", "to": { "name": "In Progress" } },
            { "id": "31", "name": "Finish", "to": { "name": "Done" } }
          ]
        }
        """;

        Assert.Equal("31", JiraRequests.FindTransitionId(transitions, "Done"));
        Assert.Null(JiraRequests.FindTransitionId(transitions, "Blocked"));
    }
}
