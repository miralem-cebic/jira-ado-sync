namespace JiraAdoSync.Core.Model;

/// <summary>
/// Persisted state for one linked item pair. The host stores this (e.g. in Azure Table Storage).
/// <see cref="LastWriter"/> is the side whose change was applied last.
/// </summary>
public sealed record SyncState(
    string JiraKey,
    string AdoWorkItemId,
    string LastSyncedHash,
    DateTimeOffset LastChangedAt,
    Side LastWriter);
