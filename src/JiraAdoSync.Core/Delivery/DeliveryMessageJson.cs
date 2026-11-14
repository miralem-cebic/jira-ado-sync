using System.Text.Json;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Delivery;

/// <summary>
/// The JSON form of a <see cref="DeliveryMessage"/> that the queue carries: <c>{"source":"jira","blobName":"jira/…"}</c>.
/// Read by hand, so that a missing or unknown field is an error and not a silent default.
/// </summary>
public static class DeliveryMessageJson
{
    /// <summary>Returns the message as JSON.</summary>
    public static string Serialize(DeliveryMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new JsonObject
        {
            ["source"] = SourceName(message.Source),
            ["blobName"] = message.BlobName,
        }.ToJsonString();
    }

    /// <summary>Reads a message. Throws <see cref="JsonException"/> for anything that is not a message of this kind.</summary>
    public static DeliveryMessage Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A delivery message is a JSON object.");
        }

        if (!root.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A delivery message has no source.");
        }

        var side = source.GetString() switch
        {
            "jira" => Side.Jira,
            "ado" => Side.Ado,
            _ => throw new JsonException("A delivery message has an unknown source."),
        };

        if (!root.TryGetProperty("blobName", out var blobName)
            || blobName.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(blobName.GetString()))
        {
            throw new JsonException("A delivery message has no blob name.");
        }

        return new DeliveryMessage(side, blobName.GetString()!);
    }

    private static string SourceName(Side source) => source switch
    {
        Side.Jira => "jira",
        Side.Ado => "ado",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown side."),
    };
}
