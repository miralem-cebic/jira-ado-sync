using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace JiraAdoSync.Adapters.Tests;

/// <summary>Answers requests with queued replies and records what was sent. The inner handler of the clients under test.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _replies = new();

    public List<CapturedRequest> Requests { get; } = [];

    public void Enqueue(HttpResponseMessage reply) => _replies.Enqueue(reply);

    public static HttpResponseMessage Reply(HttpStatusCode status, string? json = null, TimeSpan? retryAfter = null)
    {
        var reply = new HttpResponseMessage(status)
        {
            Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json"),
        };

        if (retryAfter is { } wait)
        {
            reply.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        }

        return reply;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri!.PathAndQuery,
            body,
            request.Content?.Headers.ContentType?.MediaType,
            request.Headers.Authorization?.ToString()));

        if (_replies.Count == 0)
        {
            throw new InvalidOperationException($"No reply queued for {request.Method} {request.RequestUri}.");
        }

        return _replies.Dequeue();
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    string PathAndQuery,
    string? Body,
    string? ContentType,
    string? Authorization);

/// <summary>Records the waits instead of sleeping, and runs them at once.</summary>
internal sealed class RecordingTimeProvider : TimeProvider
{
    public List<TimeSpan> Delays { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Delays.Add(dueTime);
        _ = Task.Run(() => callback(state));
        return new FiredTimer();
    }

    private sealed class FiredTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>A random source with a fixed value. 0.5 gives a jitter factor of exactly 1.0.</summary>
internal sealed class FixedRandom(double value) : Random
{
    public override double NextDouble() => value;
}
