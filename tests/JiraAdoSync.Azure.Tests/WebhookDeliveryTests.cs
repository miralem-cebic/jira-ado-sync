using System.Security.Cryptography;
using System.Text;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Azure.Tests;

public class WebhookDeliveryTests
{
    private const string JiraSecret = "jira-secret";
    private const string AdoUserName = "hook-user";
    private const string AdoPassword = "hook-password";

    private const string JiraUpdate =
        "{\"webhookEvent\":\"jira:issue_updated\",\"timestamp\":1767261600000,\"user\":{\"accountId\":\"someone-else\"}," +
        "\"issue\":{\"key\":\"PROJ-1\",\"fields\":{\"summary\":\"New title\",\"status\":{\"name\":\"In Progress\"}," +
        "\"project\":{\"key\":\"PROJ\"}}}}";

    private static (WebhookReceiver Receiver, RecordingRawStore Store, RecordingDispatcher Queue) Create()
    {
        var store = new RecordingRawStore();
        var queue = new RecordingDispatcher();
        var receiver = new WebhookReceiver(new WebhookSecrets(JiraSecret, AdoUserName, AdoPassword), store, queue);
        return (receiver, store, queue);
    }

    private static string Sign(byte[] body) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(JiraSecret), body));

    [Fact]
    public async Task AuthenticDeliveryIsAccepted()
    {
        var (receiver, store, queue) = Create();
        var body = Encoding.UTF8.GetBytes(JiraUpdate);

        var answer = await WebhookDelivery.HandleAsync(
            new MemoryStream(body), body.Length, Side.Jira, Sign(body), null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(202, answer.StatusCode);
        Assert.Equal(ReceiveResult.Accepted, answer.Result);
        Assert.False(answer.TooLarge);
        Assert.False(answer.Failed);
        Assert.Equal(1, store.Count);
        Assert.Single(queue.Messages);
    }

    [Fact]
    public async Task DeclaredLengthOverTheLimitIsAnsweredWithoutReadingTheBody()
    {
        var (receiver, store, _) = Create();
        var stream = new MemoryStream(new byte[10]);

        var answer = await WebhookDelivery.HandleAsync(
            stream, WebhookLimits.MaxBodyBytes + 1L, Side.Jira, null, null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(413, answer.StatusCode);
        Assert.True(answer.TooLarge);
        Assert.Equal(0, stream.Position);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task BodyOverTheLimitWithoutDeclaredLengthIsAnsweredWith413AndNothingIsStored()
    {
        var (receiver, store, queue) = Create();
        var body = new byte[WebhookLimits.MaxBodyBytes + 1];

        var answer = await WebhookDelivery.HandleAsync(
            new MemoryStream(body), null, Side.Jira, null, null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(413, answer.StatusCode);
        Assert.True(answer.TooLarge);
        Assert.Equal(0, store.Count);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task BodyThatCannotBeReadIsAnsweredWith503AndKeepsTheError()
    {
        var (receiver, store, _) = Create();

        var answer = await WebhookDelivery.HandleAsync(
            new UnreadableStream(), null, Side.Jira, null, null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(503, answer.StatusCode);
        Assert.True(answer.Failed);
        Assert.IsType<IOException>(answer.Error);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task StorageFailureIsAnsweredWith503()
    {
        var (receiver, store, queue) = Create();
        store.FailStores = true;
        var body = Encoding.UTF8.GetBytes(JiraUpdate);

        var answer = await WebhookDelivery.HandleAsync(
            new MemoryStream(body), body.Length, Side.Jira, Sign(body), null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(503, answer.StatusCode);
        Assert.Empty(queue.Messages);
    }

    [Fact]
    public async Task UnauthenticatedDeliveryIsAnsweredWith401()
    {
        var (receiver, store, _) = Create();
        var body = Encoding.UTF8.GetBytes(JiraUpdate);

        var answer = await WebhookDelivery.HandleAsync(
            new MemoryStream(body), body.Length, Side.Jira, "sha256=" + new string('0', 64), null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(401, answer.StatusCode);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task AuthenticBodyThatIsNotJsonIsAnsweredWith400()
    {
        var (receiver, store, _) = Create();
        var body = Encoding.UTF8.GetBytes("not json");

        var answer = await WebhookDelivery.HandleAsync(
            new MemoryStream(body), body.Length, Side.Jira, Sign(body), null, receiver, TestContext.Current.CancellationToken);

        Assert.Equal(400, answer.StatusCode);
        Assert.Equal(0, store.Count);
    }

    [Theory]
    [InlineData("12", 12L)]
    [InlineData("0", 0L)]
    [InlineData("-1", null)]
    [InlineData("abc", null)]
    [InlineData(null, null)]
    public void ContentLengthIsParsedOnlyWhenItIsAPlainNumber(string? header, long? expected)
    {
        Assert.Equal(expected, WebhookDelivery.ParseContentLength(header));
    }
}
