using JiraAdoSync.Core.Delivery;

namespace JiraAdoSync.Azure;

/// <summary>Decides whether the queue must retry a processed delivery (ADR 0004).</summary>
public static class DeliveryRetry
{
    /// <summary>True when another writer saved the pair first. The host then throws, so that the queue retries the message.</summary>
    public static bool IsRequired(DeliveryOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return outcome is DeliveryOutcome.RetryLater;
    }
}
