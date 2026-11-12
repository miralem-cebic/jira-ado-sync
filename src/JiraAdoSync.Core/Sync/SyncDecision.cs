using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Sync;

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

public abstract record SyncDecision
{
    private SyncDecision() { }

    public sealed record Skip(SkipReason Reason) : SyncDecision;

    /// <summary>The target must be written with <see cref="Item"/>. <see cref="Hash"/> becomes the new last synced hash.</summary>
    public sealed record Apply(CanonicalItem Item, string Hash) : SyncDecision;
}
