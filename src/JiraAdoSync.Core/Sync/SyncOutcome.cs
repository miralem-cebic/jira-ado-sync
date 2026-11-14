using System.Diagnostics.CodeAnalysis;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Sync;

/// <summary>What <see cref="SyncProcessor"/> did with one change.</summary>
[SuppressMessage(
    "Design",
    "CA1034:Nested types should not be visible",
    Justification = "Closed union: only nested types can call the private constructor, so no other case can exist.")]
public abstract record SyncOutcome
{
    private SyncOutcome() { }

    /// <summary>The target was written and the new state was saved.</summary>
    public sealed record Written : SyncOutcome;

    /// <summary>Nothing was written. <paramref name="Reason"/> says why.</summary>
    /// <param name="Reason">Why the change is not synced.</param>
    public sealed record Skipped(SkipReason Reason) : SyncOutcome;

    /// <summary>
    /// The target was written, but another writer saved the pair first, so the state was not saved. The change is
    /// retried and decided again from the new state.
    /// </summary>
    public sealed record Conflict : SyncOutcome;
}

/// <summary>A change received from one side, with the id of the changed item on that side.</summary>
/// <param name="Source">The side the change came from.</param>
/// <param name="SourceItemId">The Jira issue key for a Jira change, or the Azure DevOps work item id for an ADO change.</param>
/// <param name="Event">The change, in the source side's vocabulary.</param>
public sealed record SourceChange(Side Source, string SourceItemId, SyncEvent Event);
