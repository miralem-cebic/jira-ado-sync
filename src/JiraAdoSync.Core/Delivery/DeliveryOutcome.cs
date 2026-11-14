using System.Diagnostics.CodeAnalysis;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Core.Delivery;

/// <summary>Why the processor did not sync a delivery. A dropped delivery is not retried, because a retry cannot change it.</summary>
public enum DropReason
{
    /// <summary>No body is stored under the message's blob name. It expired or was never stored.</summary>
    BodyMissing,

    /// <summary>The stored body is not JSON.</summary>
    NotJson,

    /// <summary>The event type is not one the sync handles.</summary>
    UnsupportedEvent,

    /// <summary>The Jira project or ADO project of the change has no mapping in the configuration.</summary>
    UnmappedProject,
}

/// <summary>What <see cref="DeliveryProcessor.ProcessAsync"/> did with one delivery.</summary>
[SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Closed union: only nested types can call the private constructor, so no other case can exist.")]
public abstract record DeliveryOutcome
{
    private DeliveryOutcome() { }

    /// <summary>The change went through the sync. <paramref name="Outcome"/> says whether it was written or skipped.</summary>
    /// <param name="Outcome">The result of the sync processor.</param>
    /// <param name="SourceItemId">The Jira issue key or the ADO work item id of the changed item. It is not personal data.</param>
    public sealed record Processed(SyncOutcome Outcome, string SourceItemId) : DeliveryOutcome;

    /// <summary>The delivery cannot be synced. The queue completes the message.</summary>
    /// <param name="Reason">Why the delivery is dropped.</param>
    public sealed record Dropped(DropReason Reason) : DeliveryOutcome;

    /// <summary>Another writer changed the pair first. The host throws, so that the queue retries the message (ADR 0004).</summary>
    /// <param name="SourceItemId">The Jira issue key or the ADO work item id of the item, for the log.</param>
    public sealed record RetryLater(string SourceItemId) : DeliveryOutcome;
}
