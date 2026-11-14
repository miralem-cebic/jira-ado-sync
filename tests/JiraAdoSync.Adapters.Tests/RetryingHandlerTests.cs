using System.Net;
using System.Text;
using static JiraAdoSync.Adapters.Tests.StubHttpHandler;

namespace JiraAdoSync.Adapters.Tests;

public class RetryingHandlerTests
{
    private static (HttpClient Client, StubHttpHandler Stub, RecordingTimeProvider Time) Create(double jitter = 0.5)
    {
        var stub = new StubHttpHandler();
        var time = new RecordingTimeProvider();
        var handler = new RetryingHandler(new RetryOptions(), time, new FixedRandom(jitter)) { InnerHandler = stub };
        return (new HttpClient(handler) { BaseAddress = new Uri("https://dev.example.test/") }, stub, time);
    }

    [Fact]
    public async Task SuccessIsReturnedOnceWithoutWaiting()
    {
        var (client, stub, time) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var response = await client.GetAsync(new Uri("Project/_apis/wit/workitems/1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task RateLimitedRequestWaitsForRetryAfterAndIsRetried()
    {
        var (client, stub, time) = Create();
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(5)));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var response = await client.GetAsync(new Uri("rest/api/3/issue/PROJ-1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
        // Retry-After is the minimum wait and replaces the backoff.
        Assert.Equal([TimeSpan.FromSeconds(5)], time.Delays);
    }

    [Fact]
    public async Task RateLimitWithoutRetryAfterBacksOffExponentially()
    {
        var (client, stub, time) = Create(jitter: 0.5);
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests));
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var response = await client.PutAsync(new Uri("rest/api/3/issue/PROJ-1", UriKind.Relative), new StringContent("{}"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], time.Delays);
    }

    [Fact]
    public async Task BackoffIsScaledByTheLowerJitterBound()
    {
        var (client, stub, time) = Create(jitter: 0.0);
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var response = await client.GetAsync(new Uri("rest/api/3/issue/PROJ-1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The factor is 0.7 at the lower bound of the random range.
        Assert.Equal([TimeSpan.FromMilliseconds(700)], time.Delays);
    }

    [Fact]
    public async Task GivesUpAfterFourAttemptsAndReturnsTheRateLimit()
    {
        var (client, stub, time) = Create(jitter: 0.5);
        for (var i = 0; i < 4; i++)
        {
            stub.Enqueue(Reply(HttpStatusCode.TooManyRequests));
        }

        using var response = await client.GetAsync(new Uri("rest/api/3/issue/PROJ-1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(4, stub.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], time.Delays);
    }

    [Fact]
    public async Task RetryAfterLongerThanTheMaximumIsNotWaitedFor()
    {
        var (client, stub, time) = Create();
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests, retryAfter: TimeSpan.FromSeconds(1847)));

        using var response = await client.GetAsync(new Uri("rest/api/3/issue/PROJ-1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task ReadIsRetriedOnServiceUnavailable()
    {
        var (client, stub, _) = Create();
        stub.Enqueue(Reply(HttpStatusCode.ServiceUnavailable));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var response = await client.GetAsync(new Uri("Project/_apis/wit/workitems/1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
    }

    [Fact]
    public async Task WriteIsNotRetriedOnServiceUnavailable()
    {
        var (client, stub, time) = Create();
        stub.Enqueue(Reply(HttpStatusCode.ServiceUnavailable));

        using var request = new HttpRequestMessage(HttpMethod.Patch, "Project/_apis/wit/workitems/1")
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json-patch+json"),
        };
        using var response = await client.SendAsync(request, CancellationToken.None);

        // The outcome of a failed write is unknown. The queue decides again, so the handler does not retry it.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task DelayedSuccessWithRetryAfterIsHonouredBeforeReturning()
    {
        var (client, stub, time) = Create();
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}", retryAfter: TimeSpan.FromSeconds(3)));

        using var response = await client.GetAsync(new Uri("Project/_apis/wit/workitems/1", UriKind.Relative), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(stub.Requests);
        Assert.Equal([TimeSpan.FromSeconds(3)], time.Delays);
    }

    [Fact]
    public async Task RetriedRequestSendsTheSameBodyAgain()
    {
        var (client, stub, _) = Create();
        stub.Enqueue(Reply(HttpStatusCode.TooManyRequests));
        stub.Enqueue(Reply(HttpStatusCode.OK, "{}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/api/3/issue/PROJ-1/transitions")
        {
            Content = new StringContent("{\"transition\":{\"id\":\"31\"}}", Encoding.UTF8, "application/json"),
        };
        using var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.Requests.Count);
        Assert.Equal(stub.Requests[0].Body, stub.Requests[1].Body);
        Assert.Equal("application/json", stub.Requests[1].ContentType);
    }
}
