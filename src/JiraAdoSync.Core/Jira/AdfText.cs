using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JiraAdoSync.Core.Jira;

/// <summary>
/// Converts Jira Cloud's Atlassian Document Format (ADF) to plain text and back.
/// Limitation: formatting such as bold, links and lists is dropped in the plain-text form.
/// </summary>
public static class AdfText
{
    public static string? ToPlainText(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.String)
        {
            return node.GetString();
        }

        if (node.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var builder = new StringBuilder();
        Append(node, builder);
        return builder.ToString().TrimEnd('\n');
    }

    public static JsonObject FromPlainText(string? text)
    {
        var content = new JsonArray();

        foreach (var line in (text ?? string.Empty).Replace("\r\n", "\n").Split('\n'))
        {
            var paragraph = new JsonObject { ["type"] = JsonValue.Create("paragraph") };

            if (line.Length > 0)
            {
                paragraph["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = JsonValue.Create("text"),
                    ["text"] = JsonValue.Create(line),
                });
            }

            content.Add(paragraph);
        }

        return new JsonObject
        {
            ["type"] = JsonValue.Create("doc"),
            ["version"] = JsonValue.Create(1),
            ["content"] = content,
        };
    }

    private static void Append(JsonElement node, StringBuilder builder)
    {
        var type = JsonHelpers.GetString(node, "type");

        if (type == "text")
        {
            builder.Append(JsonHelpers.GetString(node, "text"));
            return;
        }

        if (type == "hardBreak")
        {
            builder.Append('\n');
            return;
        }

        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in content.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Object)
                {
                    Append(child, builder);
                }
            }
        }

        if (type == "paragraph" || type == "heading" || type == "codeBlock")
        {
            builder.Append('\n');
        }
    }
}
