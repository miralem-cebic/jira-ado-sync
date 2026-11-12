using System.Globalization;
using System.Text.Json;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Ado;

/// <summary>An Azure DevOps work item change, ready for the sync engine.</summary>
public sealed record AdoWorkItemChange(string ProjectName, string WorkItemId, int Rev, SyncEvent Event);

/// <summary>
/// Parses Azure DevOps service hook payloads for work item created and updated events.
/// Returns null for other events, so the caller can acknowledge and ignore them.
/// Throws <see cref="JsonException"/> only if the body is not JSON at all.
/// </summary>
public static class AdoWebhookParser
{
    private static readonly string[] SupportedEvents = { "workitem.created", "workitem.updated" };

    /// <param name="json">Raw service hook body.</param>
    /// <param name="serviceIdentityUniqueName">uniqueName of the identity the sync writes as. Its changes are echoes.</param>
    public static AdoWorkItemChange? Parse(string json, string serviceIdentityUniqueName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var eventType = JsonHelpers.GetString(root, "eventType");
        if (eventType is null || Array.IndexOf(SupportedEvents, eventType) < 0)
        {
            return null;
        }

        if (!JsonHelpers.TryGetObject(root, "resource", out var resource))
        {
            return null;
        }

        var fields = MergeFields(resource);

        var workItemId = JsonHelpers.GetScalarText(resource, "workItemId")
            ?? JsonHelpers.GetScalarText(resource, "id");
        if (workItemId is null)
        {
            return null;
        }

        var rev = JsonHelpers.GetInt(resource, "rev");
        if (rev is null && JsonHelpers.TryGetObject(resource, "revision", out var revision))
        {
            rev = JsonHelpers.GetInt(revision, "rev");
        }

        if (rev is null)
        {
            return null;
        }

        var projectName = GetFieldString(fields, "System.TeamProject");
        var changedAtText = GetFieldString(fields, "System.ChangedDate");
        if (projectName is null || changedAtText is null
            || !DateTimeOffset.TryParse(changedAtText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var changedAt))
        {
            return null;
        }

        // Who made this revision: prefer resource.revisedBy, fall back to System.ChangedBy.
        string? actor = null;
        if (resource.TryGetProperty("revisedBy", out var revisedBy))
        {
            actor = IdentityName(revisedBy);
        }

        if (actor is null && fields.TryGetValue("System.ChangedBy", out var changedBy))
        {
            actor = IdentityName(changedBy);
        }

        var changedByService = actor is not null
            && string.Equals(actor, serviceIdentityUniqueName, StringComparison.OrdinalIgnoreCase);

        var evt = new SyncEvent(
            Source: Side.Ado,
            ChangedByServiceIdentity: changedByService,
            ChangedAt: changedAt,
            Title: GetFieldString(fields, "System.Title") ?? string.Empty,
            Description: AdoHtml.ToPlainText(GetFieldString(fields, "System.Description")),
            Status: GetFieldString(fields, "System.State"),
            Assignee: null);

        return new AdoWorkItemChange(projectName, workItemId, rev.Value, evt);
    }

    /// <summary>
    /// Builds the item's field values after this revision. The full state comes from resource.revision.fields.
    /// Changed fields from resource.fields (newValue) override it, so the result is correct whether or not
    /// the payload contains the full state.
    /// </summary>
    private static Dictionary<string, JsonElement> MergeFields(JsonElement resource)
    {
        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (JsonHelpers.TryGetObject(resource, "revision", out var revision)
            && JsonHelpers.TryGetObject(revision, "fields", out var fullFields))
        {
            foreach (var property in fullFields.EnumerateObject())
            {
                merged[property.Name] = property.Value;
            }
        }

        if (JsonHelpers.TryGetObject(resource, "fields", out var changedFields))
        {
            foreach (var property in changedFields.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty("newValue", out var newValue))
                {
                    merged[property.Name] = newValue;
                }
            }
        }

        return merged;
    }

    private static string? GetFieldString(Dictionary<string, JsonElement> fields, string name) =>
        fields.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? IdentityName(JsonElement identity) =>
        identity.ValueKind == JsonValueKind.Object
            ? JsonHelpers.GetString(identity, "uniqueName")
            : null;
}
