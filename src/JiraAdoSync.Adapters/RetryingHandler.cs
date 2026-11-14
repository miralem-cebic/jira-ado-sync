using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace JiraAdoSync.Adapters;

/// <summary>
/// The retry policy of ADR 0005, in the HTTP pipeline of both clients. A rate limited request (429) is retried for every
/// method. 502, 503 and 504 are retried for GET only. A successful response with <c>Retry-After</c> is waited for before
/// it is returned, when the wait is short enough.
/// </summary>
public sealed class RetryingHandler : DelegatingHandler
{
    private readonly RetryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Random _random;

    /// <param name="options">Attempts and waits.</param>
    /// <param name="timeProvider">The clock for the waits and for dates in <c>Retry-After</c>.</param>
    /// <param name="random">The source of the jitter.</param>
    public RetryingHandler(RetryOptions options, TimeProvider timeProvider, Random random)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A request message cannot be sent twice, so each attempt sends a copy with the same content.
        var content = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        for (var attempt = 1; ; attempt++)
        {
            using var attemptRequest = Copy(request, content);
            var response = await base.SendAsync(attemptRequest, cancellationToken).ConfigureAwait(false);
            var retryAfter = ReadRetryAfter(response);

            if (response.IsSuccessStatusCode)
            {
                if (retryAfter is { } delay && delay <= _options.MaxDelay)
                {
                    await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
                }

                return response;
            }

            // A Retry-After longer than the maximum is not waited for: the response goes back and the queue retries later.
            if (attempt >= _options.MaxAttempts
                || !IsRetryable(response.StatusCode, request.Method)
                || retryAfter > _options.MaxDelay)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(retryAfter ?? Backoff(attempt), _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetryable(HttpStatusCode status, HttpMethod method) =>
        status == HttpStatusCode.TooManyRequests
        || (method == HttpMethod.Get
            && status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout);

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        if (header.Delta is { } delta)
        {
            return delta;
        }

        if (header.Date is { } date)
        {
            var wait = date - _timeProvider.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification = "The jitter spreads retries in time. It is not a security value.")]
    private TimeSpan Backoff(int attempt)
    {
        var jitter = 0.7 + (0.6 * _random.NextDouble());
        var seconds = _options.BaseDelay.TotalSeconds * Math.Pow(2, attempt - 1) * jitter;
        return TimeSpan.FromSeconds(Math.Min(seconds, _options.MaxDelay.TotalSeconds));
    }

    private static HttpRequestMessage Copy(HttpRequestMessage request, byte[]? content)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (content is not null)
        {
            copy.Content = new ByteArrayContent(content);
            foreach (var header in request.Content!.Headers)
            {
                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return copy;
    }
}
