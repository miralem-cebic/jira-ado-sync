using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Sync;

/// <summary>
/// Pure decision logic: given one incoming change and the stored state, decide whether to write.
/// It performs no I/O, so it can be unit-tested without Jira or Azure DevOps.
/// </summary>
public static class SyncEngine
{
    /// <summary>Decides whether <paramref name="evt"/> leads to a write on the other side.</summary>
    /// <param name="evt">The incoming change, in the source side's vocabulary.</param>
    /// <param name="state">Stored state of the linked pair, or null if the item has not been synced yet.</param>
    /// <param name="mapping">The project mapping the item belongs to.</param>
    public static SyncDecision Decide(SyncEvent evt, SyncState? state, ProjectMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(mapping);

        // 1. Echo suppression: our own writes must not trigger another write.
        if (evt.ChangedByServiceIdentity)
        {
            return new SyncDecision.Skip(SkipReason.EchoSuppressed);
        }

        var features = mapping.Features;

        // 2. Translate the status into the canonical (Jira) vocabulary.
        string? jiraStatus = null;
        if (features.Status)
        {
            jiraStatus = evt.Source == Side.Ado
                ? mapping.ToJiraStatus(evt.Status ?? string.Empty)
                : MapJiraStatus(evt.Status, mapping);

            if (jiraStatus is null)
            {
                return new SyncDecision.Skip(SkipReason.UnmappedStatus);
            }
        }

        // 3. Build the canonical item. Disabled fields become null and drop out of the hash.
        var item = new CanonicalItem(
            Title: features.Title ? evt.Title : null,
            Description: features.Description ? evt.Description : null,
            JiraStatus: jiraStatus,
            Assignee: features.Assignee ? evt.Assignee : null);

        var hash = item.Hash();

        if (state is not null)
        {
            // 4. Hash check: the synced field set has not changed since the last sync.
            if (string.Equals(state.LastSyncedHash, hash, StringComparison.Ordinal))
            {
                return new SyncDecision.Skip(SkipReason.NoChange);
            }

            // 5. Last write wins: an event older than the last applied change is stale.
            if (evt.ChangedAt < state.LastChangedAt)
            {
                return new SyncDecision.Skip(SkipReason.StaleEvent);
            }
        }

        return new SyncDecision.Apply(item, hash);
    }

    private static string? MapJiraStatus(string? jiraStatus, ProjectMapping mapping)
    {
        if (jiraStatus is null)
        {
            return null;
        }

        return mapping.StatusMap.ContainsKey(jiraStatus) ? jiraStatus : null;
    }
}
