namespace JiraAdoSync.Core.Delivery;

/// <summary>What the receiver did with a delivery. The host turns it into an HTTP response with <see cref="ReceiveResultExtensions.ToHttpStatusCode"/>.</summary>
public enum ReceiveResult
{
    /// <summary>Authenticated, JSON, stored and enqueued.</summary>
    Accepted,

    /// <summary>The credential is missing or wrong. Nothing was read beyond the authentication, nothing stored.</summary>
    Unauthorized,

    /// <summary>The body is not JSON. Nothing stored.</summary>
    NotJson,

    /// <summary>The body or the message could not be written. The vendor retries.</summary>
    Unavailable,
}

/// <summary>The HTTP status codes of ADR 0007.</summary>
public static class ReceiveResultExtensions
{
    /// <summary>Returns 202, 401, 400 or 503. No other status is used: 500, 429 and 410 change how the vendors treat the subscription.</summary>
    /// <param name="result">The result of <see cref="WebhookReceiver.ReceiveAsync"/>.</param>
    public static int ToHttpStatusCode(this ReceiveResult result) => result switch
    {
        ReceiveResult.Accepted => 202,
        ReceiveResult.Unauthorized => 401,
        ReceiveResult.NotJson => 400,
        ReceiveResult.Unavailable => 503,
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result."),
    };
}
