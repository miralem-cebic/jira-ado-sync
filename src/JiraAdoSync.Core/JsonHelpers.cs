using System.Text.Json;

namespace JiraAdoSync.Core;

/// <summary>Small, defensive accessors. Webhook payloads are external input, so nothing here throws on shape mismatches.</summary>
internal static class JsonHelpers
{
    public static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        value = default;
        return false;
    }

    public static string? GetString(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    /// <summary>Returns the text of a string or number property, e.g. work item ids that may arrive as numbers.</summary>
    public static string? GetScalarText(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.String))
        {
            return value.ToString();
        }

        return null;
    }

    public static int? GetInt(JsonElement parent, string name)
    {
        if (parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var number))
        {
            return number;
        }

        return null;
    }
}
