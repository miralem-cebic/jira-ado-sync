using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Core.Sync;

/// <summary>
/// Runs one change through the engine and, when the engine says so, writes it to the other side and saves the state.
/// It depends only on the ports, so the order of its steps is tested without HTTP or storage.
/// </summary>
/// <remarks>
/// The target is written before the state is saved (DESIGN.md, section 5). A write that throws saves nothing, and the
/// caller retries the change from the old state. A save that loses a race is reported as <see cref="SyncOutcome.Conflict"/>.
/// </remarks>
public sealed class SyncProcessor
{
    private readonly IJiraClient _jira;
    private readonly IAdoClient _ado;
    private readonly ISyncStateStore _store;

    /// <param name="jira">Writes to Jira.</param>
    /// <param name="ado">Writes to Azure DevOps.</param>
    /// <param name="store">Stores the linked pairs.</param>
    public SyncProcessor(IJiraClient jira, IAdoClient ado, ISyncStateStore store)
    {
        _jira = jira ?? throw new ArgumentNullException(nameof(jira));
        _ado = ado ?? throw new ArgumentNullException(nameof(ado));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Processes one change. Safe to call again for the same change: a repeat is skipped as <see cref="SkipReason.NoChange"/>.</summary>
    /// <param name="change">The change and the id of its item on the source side.</param>
    /// <param name="mapping">The project mapping the item belongs to.</param>
    /// <param name="cancellationToken">Cancels the processing.</param>
    public async Task<SyncOutcome> ProcessAsync(SourceChange change, ProjectMapping mapping, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(mapping);

        var stored = change.Source == Side.Jira
            ? await _store.FindByJiraKeyAsync(mapping.Id, change.SourceItemId, cancellationToken).ConfigureAwait(false)
            : await _store.FindByAdoIdAsync(mapping.Id, change.SourceItemId, cancellationToken).ConfigureAwait(false);

        var decision = SyncEngine.Decide(change.Event, stored?.State, mapping);
        if (decision is SyncDecision.Skip skip)
        {
            return new SyncOutcome.Skipped(skip.Reason);
        }

        if (stored is null)
        {
            // Creating the item on the other side is Phase 3. Phase 1 syncs linked pairs only.
            return new SyncOutcome.Skipped(SkipReason.NotLinked);
        }

        var apply = (SyncDecision.Apply)decision;
        var state = stored.State;

        if (change.Source == Side.Jira)
        {
            await _ado.ApplyAsync(state.AdoWorkItemId, apply.Item, mapping, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _jira.ApplyAsync(state.JiraKey, apply.Item, cancellationToken).ConfigureAwait(false);
        }

        var updated = new SyncState(
            state.JiraKey,
            state.AdoWorkItemId,
            apply.Hash,
            change.Event.ChangedAt,
            change.Source);

        var saved = await _store.TrySaveAsync(mapping.Id, updated, stored.ETag, cancellationToken).ConfigureAwait(false);
        return saved ? new SyncOutcome.Written() : new SyncOutcome.Conflict();
    }
}
