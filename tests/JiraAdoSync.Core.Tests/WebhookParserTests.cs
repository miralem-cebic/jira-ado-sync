using JiraAdoSync.Core.Ado;
using JiraAdoSync.Core.Jira;
using JiraAdoSync.Core.Model;
using Xunit;

namespace JiraAdoSync.Core.Tests;

public class WebhookParserTests
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

    // The full state says "New". The changed field says "Active" and must win.
    private const string AdoUpdated = """
    {
      "eventType": "workitem.updated",
      "resource": {
        "workItemId": 42,
        "rev": 6,
        "revisedBy": { "uniqueName": "someone@example.com" },
        "fields": {
          "System.State": { "oldValue": "New", "newValue": "Active" }
        },
        "revision": {
          "id": 42,
          "rev": 6,
          "fields": {
            "System.TeamProject": "Project",
            "System.Title": "Login bug",
            "System.Description": "<div>Line one</div><div>Line two</div>",
            "System.State": "New",
            "System.ChangedDate": "2026-01-01T10:05:00Z"
          }
        }
      }
    }
    """;

    [Fact]
    public void Jira_issue_updated_is_parsed_into_a_sync_event()
    {
        var change = JiraWebhookParser.Parse(JiraUpdated, serviceAccountId: "svc-account");

        Assert.NotNull(change);
        Assert.Equal("PROJ", change!.ProjectKey);
        Assert.Equal("PROJ-7", change.IssueKey);
        Assert.Equal(Side.Jira, change.Event.Source);
        Assert.Equal("Login bug", change.Event.Title);
        Assert.Equal("Line one\nLine two", change.Event.Description);
        Assert.Equal("In Progress", change.Event.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), change.Event.ChangedAt);
        Assert.False(change.Event.ChangedByServiceIdentity);
    }

    [Fact]
    public void Jira_change_by_service_account_is_flagged_as_echo()
    {
        var change = JiraWebhookParser.Parse(JiraUpdated, serviceAccountId: "acc-123");

        Assert.True(change!.Event.ChangedByServiceIdentity);
    }

    [Fact]
    public void Unsupported_jira_event_returns_null()
    {
        var json = JiraUpdated.Replace("jira:issue_updated", "jira:issue_deleted");

        Assert.Null(JiraWebhookParser.Parse(json, "svc-account"));
    }

    [Fact]
    public void Ado_update_takes_changed_field_values_over_the_full_state()
    {
        var change = AdoWebhookParser.Parse(AdoUpdated, serviceIdentityUniqueName: "svc-ado");

        Assert.NotNull(change);
        Assert.Equal("Project", change!.ProjectName);
        Assert.Equal("42", change.WorkItemId);
        Assert.Equal(6, change.Rev);
        Assert.Equal("Active", change.Event.Status);
        Assert.Equal("Login bug", change.Event.Title);
        Assert.Equal("Line one\nLine two", change.Event.Description);
        Assert.False(change.Event.ChangedByServiceIdentity);
    }

    [Fact]
    public void Ado_change_by_service_identity_is_flagged_as_echo()
    {
        var json = AdoUpdated.Replace("someone@example.com", "svc-ado");

        var change = AdoWebhookParser.Parse(json, serviceIdentityUniqueName: "svc-ado");

        Assert.True(change!.Event.ChangedByServiceIdentity);
    }

    [Fact]
    public void Unsupported_ado_event_returns_null()
    {
        var json = AdoUpdated.Replace("workitem.updated", "workitem.deleted");

        Assert.Null(AdoWebhookParser.Parse(json, "svc-ado"));
    }

    [Fact]
    public void Adf_and_html_plain_text_are_converted_consistently()
    {
        var adf = JiraUpdated;
        var jira = JiraWebhookParser.Parse(adf, "svc-account");
        var ado = AdoWebhookParser.Parse(AdoUpdated, "svc-ado");

        Assert.Equal(jira!.Event.Description, ado!.Event.Description);
    }
}
