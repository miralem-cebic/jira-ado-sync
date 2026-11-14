using System.Text.Json;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Tests;

public class DeliveryMessageJsonTests
{
    [Fact]
    public void RoundTripKeepsTheSourceAndTheBlobName()
    {
        var message = new DeliveryMessage(Side.Ado, "ado/abc123");

        var restored = DeliveryMessageJson.Deserialize(DeliveryMessageJson.Serialize(message));

        Assert.Equal(message, restored);
    }

    [Fact]
    public void SourceIsWrittenAsJiraOrAdo()
    {
        var json = DeliveryMessageJson.Serialize(new DeliveryMessage(Side.Jira, "jira/1"));

        Assert.Contains("\"source\":\"jira\"", json, StringComparison.Ordinal);
        Assert.Contains("\"blobName\":\"jira/1\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownSourceIsRejected()
    {
        Assert.Throws<JsonException>(() => DeliveryMessageJson.Deserialize("{\"source\":\"other\",\"blobName\":\"x\"}"));
    }

    [Fact]
    public void MessageWithoutBlobNameIsRejected()
    {
        Assert.Throws<JsonException>(() => DeliveryMessageJson.Deserialize("{\"source\":\"jira\"}"));
    }
}
