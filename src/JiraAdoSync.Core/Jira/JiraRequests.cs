using System.Text.Json;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Jira;

/// <summary>Request bodies for the Jira REST API v3. Pure functions, no HTTP.</summary>
public static class JiraRequests
{
    /// <summary>Body for PUT /rest/api/3/issue/{key}. Only fields that are not null are included.</summary>
    /// <param name="item">The field values to write. Null fields are left unchanged.</param>
    public static string BuildIssueUpdate(CanonicalItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var fields = new JsonObject();

        if (item.Title is not null)
        {
            fields["summary"] = JsonValue.Create(item.Title);
        }

        if (item.Description is not null)
        {
            fields["description"] = AdfText.FromPlainText(item.Description);
        }

        return new JsonObject { ["fields"] = fields }.ToJsonString();
    }

    /// <summary>
    /// Finds the transition that leads to the target status. Jira changes status through transitions, not directly.
    /// Returns null if the workflow has no transition to that status from the current state.
    /// </summary>
    public static string? FindTransitionId(string transitionsJson, string targetStatus)
    {
        using var doc = JsonDocument.Parse(transitionsJson);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("transitions", out var transitions)
            || transitions.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var transition in transitions.EnumerateArray())
        {
            var toName = JsonHelpers.TryGetObject(transition, "to", out var to)
                ? JsonHelpers.GetString(to, "name")
                : null;

            if (string.Equals(toName, targetStatus, StringComparison.Ordinal))
            {
                return JsonHelpers.GetScalarText(transition, "id");
            }
        }

        return null;
    }

    /// <summary>Body for POST /rest/api/3/issue/{key}/transitions.</summary>
    public static string BuildTransition(string transitionId) =>
        new JsonObject
        {
            ["transition"] = new JsonObject { ["id"] = JsonValue.Create(transitionId) },
        }.ToJsonString();
}
