using System.Diagnostics.CodeAnalysis;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Sync;

/// <summary>Why the engine decided not to write to the target.</summary>
public enum SkipReason
{
    /// <summary>The change was made by the sync identity itself.</summary>
    EchoSuppressed,

    /// <summary>The synced field set equals the last synced state.</summary>
    NoChange,

    /// <summary>The source status has no mapping. The change is not synced.</summary>
    UnmappedStatus,

    /// <summary>The event is older than the last applied change (last write wins).</summary>
    StaleEvent,
}

/// <summary>Result of <see cref="SyncEngine.Decide"/>: either <see cref="Skip"/> or <see cref="Apply"/>, nothing else.</summary>
[SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Closed union: only nested types can call the private constructor, so no other case can exist.")]
public abstract record SyncDecision
{
    private SyncDecision() { }

    /// <summary>Nothing is written to the target.</summary>
    /// <param name="Reason">Why the change is not synced.</param>
    public sealed record Skip(SkipReason Reason) : SyncDecision;

    /// <summary>The target must be written with <see cref="Item"/>. <see cref="Hash"/> becomes the new last synced hash.</summary>
    /// <param name="Item">The field values to write to the target.</param>
    /// <param name="Hash">Hash of <paramref name="Item"/>, to store as the last synced hash once the write succeeded.</param>
    public sealed record Apply(CanonicalItem Item, string Hash) : SyncDecision;
}
