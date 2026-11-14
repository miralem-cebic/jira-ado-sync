using Azure;
using Azure.Data.Tables;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Azure;

/// <summary>The pair store on Azure Table Storage. The save uses the row's ETag, so two writers for one pair cannot both win.</summary>
public sealed class TableSyncStateStore(TableClient table) : ISyncStateStore
{
    private readonly TableClient _table = table ?? throw new ArgumentNullException(nameof(table));

    /// <inheritdoc />
    public async Task<StoredSyncState?> FindByJiraKeyAsync(string mappingId, string jiraKey, CancellationToken cancellationToken)
    {
        var response = await _table.GetEntityIfExistsAsync<TableEntity>(mappingId, jiraKey, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return response.HasValue
            ? new StoredSyncState(SyncStateEntity.ToState(response.Value!), response.Value!.ETag.ToString())
            : null;
    }

    /// <inheritdoc />
    public async Task<StoredSyncState?> FindByAdoIdAsync(string mappingId, string adoWorkItemId, CancellationToken cancellationToken)
    {
        // The query is on the partition plus a property. There is no index on the work item id, which is acceptable at this size.
        var filter = TableClient.CreateQueryFilter($"PartitionKey eq {mappingId} and AdoWorkItemId eq {adoWorkItemId}");

        var rows = _table.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            if (!await rows.MoveNextAsync().ConfigureAwait(false))
            {
                return null;
            }

            var entity = rows.Current;
            return new StoredSyncState(SyncStateEntity.ToState(entity), entity.ETag.ToString());
        }
        finally
        {
            await rows.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TrySaveAsync(string mappingId, SyncState state, string? expectedETag, CancellationToken cancellationToken)
    {
        var entity = SyncStateEntity.ToEntity(mappingId, state);

        try
        {
            if (expectedETag is null)
            {
                await _table.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _table.UpdateEntityAsync(entity, new ETag(expectedETag), TableUpdateMode.Replace, cancellationToken)
                    .ConfigureAwait(false);
            }

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status is 404 or 409 or 412)
        {
            // 409: the row exists already. 404: it was deleted. 412: the ETag is no longer current. In each case another writer won.
            return false;
        }
    }
}
