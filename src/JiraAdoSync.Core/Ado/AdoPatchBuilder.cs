using System.Text.Json.Nodes;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Ado;

/// <summary>Builds JSON Patch bodies for the Azure DevOps Work Items REST API. Pure function, no HTTP.</summary>
public static class AdoPatchBuilder
{
    /// <summary>
    /// Body for PATCH https://dev.azure.com/{org}/{project}/_apis/wit/workitems/{id}?api-version=7.1
    /// The leading "test" on /rev makes the request fail if the item changed after the revision we read,
    /// so the caller can re-read and retry instead of overwriting a newer change.
    /// </summary>
    public static string Build(int expectedRev, CanonicalItem item, ProjectMapping mapping)
    {
        var operations = new JsonArray
        {
            Operation("test", "/rev", JsonValue.Create(expectedRev)),
        };

        if (item.Title is not null)
        {
            operations.Add(Operation("add", "/fields/System.Title", JsonValue.Create(item.Title)));
        }

        if (item.Description is not null)
        {
            operations.Add(Operation(
                "add",
                "/fields/System.Description",
                JsonValue.Create(AdoHtml.FromPlainText(item.Description))));
        }

        if (item.JiraStatus is not null)
        {
            var state = mapping.ToAdoState(item.JiraStatus)
                ?? throw new InvalidOperationException(
                    $"No ADO state is mapped for Jira status '{item.JiraStatus}'. The engine should have skipped this change.");

            operations.Add(Operation("add", "/fields/System.State", JsonValue.Create(state)));
        }

        return operations.ToJsonString();
    }

    private static JsonObject Operation(string op, string path, JsonNode? value) => new()
    {
        ["op"] = JsonValue.Create(op),
        ["path"] = JsonValue.Create(path),
        ["value"] = value,
    };
}
