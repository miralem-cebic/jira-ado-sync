using JiraAdoSync.Core.Delivery;

namespace JiraAdoSync.Azure;

/// <summary>The answer to one webhook request: the status code, what the receiver decided, and whether the request failed.</summary>
/// <param name="StatusCode">The status code to answer with (ADR 0007 and ADR 0009).</param>
/// <param name="Result">What the receiver decided, or null when the request never reached the receiver.</param>
/// <param name="TooLarge">The body is over <see cref="WebhookLimits.MaxBodyBytes"/>.</param>
/// <param name="Error">The exception that made the request fail, or null.</param>
public sealed record WebhookAnswer(int StatusCode, ReceiveResult? Result, bool TooLarge, Exception? Error)
{
    /// <summary>True when the request failed while it was read or processed. Such a request is answered with 503.</summary>
    public bool Failed => Error is not null;

    /// <summary>The answer for a decision of the receiver.</summary>
    public static WebhookAnswer ForResult(ReceiveResult result) => new(result.ToHttpStatusCode(), result, false, null);

    /// <summary>The answer for a body over the limit (ADR 0009).</summary>
    public static WebhookAnswer ForTooLarge() => new(413, null, true, null);

    /// <summary>The answer for a request that failed while it was read or processed (ADR 0007).</summary>
    public static WebhookAnswer ForFailure(Exception error) => new(503, null, false, error);
}
