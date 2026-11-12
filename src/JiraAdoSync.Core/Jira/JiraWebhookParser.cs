using System.Text.Json;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Jira;

/// <summary>A Jira issue change, ready for the sync engine.</summary>
public sealed record JiraIssueChange(string ProjectKey, string IssueKey, SyncEvent Event);

/// <summary>
/// Parses Jira Cloud webhook payloads for issue created and updated events.
/// Returns null for other events, so the caller can acknowledge and ignore them.
/// Throws <see cref="JsonException"/> only if the body is not JSON at all.
/// </summary>
public static class JiraWebhookParser
{
    private static readonly string[] SupportedEvents = { "jira:issue_created", "jira:issue_updated" };

    /// <param name="json">Raw webhook body.</param>
    /// <param name="serviceAccountId">accountId of the sync's own Jira service account. Its changes are echoes.</param>
    public static JiraIssueChange? Parse(string json, string serviceAccountId)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var webhookEvent = JsonHelpers.GetString(root, "webhookEvent");
        if (webhookEvent is null || Array.IndexOf(SupportedEvents, webhookEvent) < 0)
        {
            return null;
        }

        if (!JsonHelpers.TryGetObject(root, "issue", out var issue)
            || !JsonHelpers.TryGetObject(issue, "fields", out var fields))
        {
            return null;
        }

        var issueKey = JsonHelpers.GetString(issue, "key");
        if (issueKey is null)
        {
            return null;
        }

        // Jira always sends a millisecond timestamp for webhooks. Without it, last-write-wins cannot work.
        if (!root.TryGetProperty("timestamp", out var timestamp) || !timestamp.TryGetInt64(out var millis))
        {
            return null;
        }

        var projectKey = JsonHelpers.TryGetObject(fields, "project", out var project)
            ? JsonHelpers.GetString(project, "key")
            : null;
        projectKey ??= issueKey.Split('-')[0];

        // The actor may be missing (e.g. for automation-triggered updates). Then the echo check
        // cannot match, and the hash check in the engine prevents the loop instead.
        var actorId = JsonHelpers.TryGetObject(root, "user", out var user)
            ? JsonHelpers.GetString(user, "accountId")
            : null;
        var changedByService = actorId is not null
            && string.Equals(actorId, serviceAccountId, StringComparison.Ordinal);

        var description = fields.TryGetProperty("description", out var descriptionNode)
            ? AdfText.ToPlainText(descriptionNode)
            : null;

        var status = JsonHelpers.TryGetObject(fields, "status", out var statusNode)
            ? JsonHelpers.GetString(statusNode, "name")
            : null;

        // Assignee is not synced yet. It needs a user mapping between Jira accountIds and ADO identities.
        var evt = new SyncEvent(
            Source: Side.Jira,
            ChangedByServiceIdentity: changedByService,
            ChangedAt: DateTimeOffset.FromUnixTimeMilliseconds(millis),
            Title: JsonHelpers.GetString(fields, "summary") ?? string.Empty,
            Description: description,
            Status: status,
            Assignee: null);

        return new JiraIssueChange(projectKey, issueKey, evt);
    }
}
