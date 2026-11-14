using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Ports;

/// <summary>A stored state together with the ETag of its row, which is passed back when the row is saved.</summary>
/// <param name="State">The stored state of the pair.</param>
/// <param name="ETag">The row's ETag at the time it was read.</param>
public sealed record StoredSyncState(SyncState State, string ETag);

/// <summary>
/// Stores the linked pairs. The host implements it with Azure Table Storage (DESIGN.md, section 4): the partition key
/// is the mapping id and the row key is the Jira issue key.
/// </summary>
public interface ISyncStateStore
{
    /// <summary>Finds the pair for a Jira issue key, or returns null when the issue is not linked.</summary>
    Task<StoredSyncState?> FindByJiraKeyAsync(string mappingId, string jiraKey, CancellationToken cancellationToken);

    /// <summary>Finds the pair for an Azure DevOps work item id, or returns null when the item is not linked.</summary>
    Task<StoredSyncState?> FindByAdoIdAsync(string mappingId, string adoWorkItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves <paramref name="state"/>. The save succeeds only when the stored row still has <paramref name="expectedETag"/>,
    /// so that two concurrent events for one pair cannot both win. A null ETag means the row must not exist yet.
    /// Returns false when the row changed first.
    /// </summary>
    Task<bool> TrySaveAsync(string mappingId, SyncState state, string? expectedETag, CancellationToken cancellationToken);
}
