using Azure.Data.Tables;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Azure;

/// <summary>Maps a <see cref="SyncState"/> to its row in the <c>SyncItems</c> table (DESIGN.md, section 4).</summary>
public static class SyncStateEntity
{
    /// <summary>The row of a linked pair: the mapping id is the partition key, the Jira issue key the row key.</summary>
    public static TableEntity ToEntity(string mappingId, SyncState state)
    {
        ArgumentException.ThrowIfNullOrEmpty(mappingId);
        ArgumentNullException.ThrowIfNull(state);

        return new TableEntity(mappingId, state.JiraKey)
        {
            ["AdoWorkItemId"] = state.AdoWorkItemId,
            ["LastSyncedHash"] = state.LastSyncedHash,
            ["LastChangedAt"] = state.LastChangedAt,
            ["LastWriter"] = state.LastWriter == Side.Jira ? "jira" : "ado",
        };
    }

    /// <summary>Reads the state back from its row. Throws <see cref="InvalidOperationException"/> when a column is missing.</summary>
    public static SyncState ToState(TableEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var changedAt = entity.GetDateTimeOffset("LastChangedAt")
            ?? throw new InvalidOperationException("The SyncItems row has no LastChangedAt.");

        return new SyncState(
            entity.RowKey,
            Required(entity, "AdoWorkItemId"),
            Required(entity, "LastSyncedHash"),
            changedAt,
            Required(entity, "LastWriter") == "jira" ? Side.Jira : Side.Ado);
    }

    private static string Required(TableEntity entity, string column) =>
        entity.GetString(column) ?? throw new InvalidOperationException($"The SyncItems row has no {column}.");
}
