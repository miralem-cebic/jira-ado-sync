using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Core.Tests;

internal sealed class FakeJiraClient : IJiraClient
{
    public List<(string IssueKey, CanonicalItem Item)> Applied { get; } = [];

    public Exception? FailWith { get; init; }

    public Task ApplyAsync(string issueKey, CanonicalItem item, CancellationToken cancellationToken)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Applied.Add((issueKey, item));
        return Task.CompletedTask;
    }
}

internal sealed class FakeAdoClient : IAdoClient
{
    public List<(string WorkItemId, CanonicalItem Item)> Applied { get; } = [];

    public Exception? FailWith { get; init; }

    public Task ApplyAsync(string workItemId, CanonicalItem item, ProjectMapping mapping, CancellationToken cancellationToken)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Applied.Add((workItemId, item));
        return Task.CompletedTask;
    }
}

/// <summary>Stores states per mapping with an ETag per row, the way Table Storage does.</summary>
internal sealed class InMemorySyncStateStore : ISyncStateStore
{
    private readonly Dictionary<string, (SyncState State, int Version)> _rows = new(StringComparer.Ordinal);

    /// <summary>When set, another writer saves the row between the read and the next write, so that write must fail.</summary>
    public bool ConcurrentWriteBeforeNextSave { get; set; }

    public void Seed(string mappingId, SyncState state) =>
        _rows[Key(mappingId, state.JiraKey)] = (state, 1);

    public Task<StoredSyncState?> FindByJiraKeyAsync(string mappingId, string jiraKey, CancellationToken cancellationToken) =>
        Task.FromResult(Find(mappingId, jiraKey));

    public Task<StoredSyncState?> FindByAdoIdAsync(string mappingId, string adoWorkItemId, CancellationToken cancellationToken)
    {
        var match = _rows
            .Where(pair => pair.Key.StartsWith(mappingId + "|", StringComparison.Ordinal))
            .Select(pair => pair.Value)
            .FirstOrDefault(row => row.State.AdoWorkItemId == adoWorkItemId);

        return Task.FromResult(match.State is null ? null : new StoredSyncState(match.State, Etag(match.Version)));
    }

    public Task<bool> TrySaveAsync(string mappingId, SyncState state, string? expectedETag, CancellationToken cancellationToken)
    {
        var key = Key(mappingId, state.JiraKey);
        var current = _rows.TryGetValue(key, out var row) ? Etag(row.Version) : null;

        if (ConcurrentWriteBeforeNextSave)
        {
            ConcurrentWriteBeforeNextSave = false;
            _rows[key] = (row.State, row.Version + 1);
            current = Etag(row.Version + 1);
        }

        if (!string.Equals(current, expectedETag, StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        _rows[key] = (state, (row.Version) + 1);
        return Task.FromResult(true);
    }

    public SyncState? Get(string mappingId, string jiraKey) =>
        _rows.TryGetValue(Key(mappingId, jiraKey), out var row) ? row.State : null;

    private StoredSyncState? Find(string mappingId, string jiraKey) =>
        _rows.TryGetValue(Key(mappingId, jiraKey), out var row)
            ? new StoredSyncState(row.State, Etag(row.Version))
            : null;

    private static string Key(string mappingId, string jiraKey) => mappingId + "|" + jiraKey;

    private static string Etag(int version) => "W/\"" + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"";
}
