using System.Security.Cryptography;
using System.Text;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Tests;

public class WebhookReceiverTests
{
    private const string JiraSecret = "jira-secret";
    private const string AdoUserName = "sync-hook";
    private const string AdoPassword = "hook-password";

    private const string JiraUpdate =
        "{\"webhookEvent\":\"jira:issue_updated\",\"timestamp\":1767261600000,\"user\":{\"accountId\":\"someone-else\"}," +
        "\"issue\":{\"key\":\"PROJ-1\",\"fields\":{\"summary\":\"New title\",\"status\":{\"name\":\"In Progress\"}," +
        "\"project\":{\"key\":\"PROJ\"}}}}";

    private const string AdoUpdate =
        "{\"eventType\":\"workitem.updated\",\"resource\":{\"workItemId\":42,\"rev\":8," +
        "\"revisedBy\":{\"uniqueName\":\"someone@example.test\"},\"revision\":{\"fields\":{" +
        "\"System.TeamProject\":\"Project\",\"System.ChangedDate\":\"2026-01-01T10:05:00Z\"," +
        "\"System.Title\":\"From ADO\",\"System.State\":\"Closed\"}}}}";

    private const string Comment = "{\"webhookEvent\":\"comment_created\",\"comment\":{\"id\":\"1\"}}";

    private static readonly WebhookSecrets Secrets = new(JiraSecret, AdoUserName, AdoPassword);

    private static (WebhookReceiver Receiver, InMemoryRawDeliveryStore Store, FakeDeliveryDispatcher Queue) Create()
    {
        var store = new InMemoryRawDeliveryStore();
        var queue = new FakeDeliveryDispatcher(store);
        return (new WebhookReceiver(Secrets, store, queue), store, queue);
    }

    private static WebhookRequest JiraRequest(string body, string? signature = null) =>
        new(Side.Jira, Encoding.UTF8.GetBytes(body), signature ?? Sign(body), null);

    private static WebhookRequest AdoRequest(string body, string? authorization = null) =>
        new(Side.Ado, Encoding.UTF8.GetBytes(body), null, authorization ?? Basic(AdoUserName, AdoPassword));

    private static string Sign(string body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(JiraSecret), Encoding.UTF8.GetBytes(body)));

    private static string Basic(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password));

    [Fact]
    public async Task AuthenticJiraDeliveryIsStoredBeforeItIsEnqueued()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(JiraRequest(JiraUpdate), CancellationToken.None);

        Assert.Equal(ReceiveResult.Accepted, result);
        var message = Assert.Single(queue.Messages);
        Assert.Equal(Side.Jira, message.Source);
        Assert.StartsWith("jira/", message.BlobName, StringComparison.Ordinal);
        Assert.True(store.Contains(message.BlobName));
        Assert.True(queue.StoredWhenEnqueued, "the body must be stored before its message is enqueued");
    }

    [Fact]
    public async Task AuthenticAdoDeliveryIsAccepted()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(AdoRequest(AdoUpdate), CancellationToken.None);

        Assert.Equal(ReceiveResult.Accepted, result);
        var message = Assert.Single(queue.Messages);
        Assert.Equal(Side.Ado, message.Source);
        Assert.True(store.Contains(message.BlobName));
    }

    [Fact]
    public async Task UnauthenticatedJiraDeliveryIsRejectedAndNothingIsStored()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(
            JiraRequest(JiraUpdate, "sha256=" + new string('0', 64)),
            CancellationToken.None);

        Assert.Equal(ReceiveResult.Unauthorized, result);
        Assert.Equal(0, store.Count);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task WrongAdoCredentialsAreRejectedAndNothingIsStored()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(
            AdoRequest(AdoUpdate, Basic(AdoUserName, "wrong")),
            CancellationToken.None);

        Assert.Equal(ReceiveResult.Unauthorized, result);
        Assert.Equal(0, store.Count);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task AuthenticBodyThatIsNotJsonIsRejectedAndNothingIsStored()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(JiraRequest("not json"), CancellationToken.None);

        Assert.Equal(ReceiveResult.NotJson, result);
        Assert.Equal(0, store.Count);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task EventTypeTheSyncDoesNotHandleIsStillAcceptedSoTheVendorDoesNotRetryIt()
    {
        var (receiver, store, queue) = Create();

        var result = await receiver.ReceiveAsync(JiraRequest(Comment), CancellationToken.None);

        Assert.Equal(ReceiveResult.Accepted, result);
        Assert.Single(queue.Messages);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task StorageFailureIsUnavailableAndNothingIsEnqueued()
    {
        var (receiver, store, queue) = Create();
        store.FailStores = true;

        var result = await receiver.ReceiveAsync(JiraRequest(JiraUpdate), CancellationToken.None);

        Assert.Equal(ReceiveResult.Unavailable, result);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task QueueFailureIsUnavailableAndTheStoredBodyIsKept()
    {
        var (receiver, store, queue) = Create();
        queue.FailEnqueue = true;

        var result = await receiver.ReceiveAsync(JiraRequest(JiraUpdate), CancellationToken.None);

        Assert.Equal(ReceiveResult.Unavailable, result);
        Assert.Empty(queue.Messages);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task RedeliveryOfTheSameBodyKeepsOneStoredBlob()
    {
        var (receiver, store, queue) = Create();

        var first = await receiver.ReceiveAsync(JiraRequest(JiraUpdate), CancellationToken.None);
        var second = await receiver.ReceiveAsync(JiraRequest(JiraUpdate), CancellationToken.None);

        Assert.Equal(ReceiveResult.Accepted, first);
        Assert.Equal(ReceiveResult.Accepted, second);
        Assert.Equal(1, store.Count);
        Assert.Equal(2, queue.Messages.Count);
        Assert.Equal(queue.Messages[0].BlobName, queue.Messages[1].BlobName);
    }

    [Theory]
    [InlineData(ReceiveResult.Accepted, 202)]
    [InlineData(ReceiveResult.Unauthorized, 401)]
    [InlineData(ReceiveResult.NotJson, 400)]
    [InlineData(ReceiveResult.Unavailable, 503)]
    public void EachResultHasTheStatusCodeOfADR0007(ReceiveResult result, int expected)
    {
        Assert.Equal(expected, result.ToHttpStatusCode());
    }

    private static readonly int[] FailureStatusCodes = [410, 429, 500];

    [Fact]
    public void NoResultIsAnsweredWithAStatusTheVendorsTreatAsAFailureOfTheSync()
    {
        // 500 and 429 put an ADO subscription on probation, and 410 disables it (ADR 0003, ADR 0007).
        foreach (var result in Enum.GetValues<ReceiveResult>())
        {
            Assert.DoesNotContain(result.ToHttpStatusCode(), FailureStatusCodes);
        }
    }
}
