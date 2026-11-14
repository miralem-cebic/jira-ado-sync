using System.Text;
using System.Text.Json;
using JiraAdoSync.Core.Ado;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Jira;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;
using JiraAdoSync.Core.Sync;

namespace JiraAdoSync.Core.Delivery;

/// <summary>
/// The processing half of the webhook path (ADR 0004): load the stored body, parse it with the webhook parsers, find the
/// project mapping and run the sync processor. A body that can never succeed is dropped. A failure that can succeed later
/// is thrown, so that the queue retries it.
/// </summary>
public sealed class DeliveryProcessor
{
    private readonly IRawDeliveryStore _store;
    private readonly SyncProcessor _sync;
    private readonly SyncIdentities _identities;
    private readonly IReadOnlyList<ProjectMapping> _mappings;

    /// <param name="store">Keeps the raw bodies.</param>
    /// <param name="sync">Runs the sync for one change.</param>
    /// <param name="identities">The identities the sync writes as, for the echo check.</param>
    /// <param name="mappings">The project mappings from the configuration.</param>
    public DeliveryProcessor(IRawDeliveryStore store, SyncProcessor sync, SyncIdentities identities, IReadOnlyList<ProjectMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentException.ThrowIfNullOrEmpty(identities.JiraServiceAccountId);
        ArgumentException.ThrowIfNullOrEmpty(identities.AdoServiceIdentity);

        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
        _identities = identities;
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    /// <summary>Processes one queued delivery. Safe to call again for the same message.</summary>
    /// <param name="message">The message the receiver enqueued.</param>
    /// <param name="cancellationToken">Cancels the processing.</param>
    public async Task<DeliveryOutcome> ProcessAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var body = await _store.LoadAsync(message.BlobName, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return new DeliveryOutcome.Dropped(DropReason.BodyMissing);
        }

        Parsed? parsed;
        try
        {
            parsed = Parse(message.Source, Encoding.UTF8.GetString(body));
        }
        catch (JsonException)
        {
            return new DeliveryOutcome.Dropped(DropReason.NotJson);
        }

        if (parsed is null)
        {
            return new DeliveryOutcome.Dropped(DropReason.UnsupportedEvent);
        }

        var mapping = FindMapping(message.Source, parsed.ProjectName);
        if (mapping is null)
        {
            return new DeliveryOutcome.Dropped(DropReason.UnmappedProject);
        }

        var outcome = await _sync.ProcessAsync(parsed.Change, mapping, cancellationToken).ConfigureAwait(false);
        return outcome is SyncOutcome.Conflict
            ? new DeliveryOutcome.RetryLater()
            : new DeliveryOutcome.Processed(outcome);
    }

    /// <summary>Parses the body with the webhook parser of its source. Returns null for an event the parser does not handle.</summary>
    private Parsed? Parse(Side source, string json) => source switch
    {
        Side.Jira => ParseJira(json),
        Side.Ado => ParseAdo(json),
        _ => null,
    };

    private Parsed? ParseJira(string json)
    {
        var change = JiraWebhookParser.Parse(json, _identities.JiraServiceAccountId);
        return change is null
            ? null
            : new Parsed(new SourceChange(Side.Jira, change.IssueKey, change.Event), change.ProjectKey);
    }

    private Parsed? ParseAdo(string json)
    {
        var change = AdoWebhookParser.Parse(json, _identities.AdoServiceIdentity);
        return change is null
            ? null
            : new Parsed(new SourceChange(Side.Ado, change.WorkItemId, change.Event), change.ProjectName);
    }

    private ProjectMapping? FindMapping(Side source, string projectName) => source == Side.Jira
        ? _mappings.FirstOrDefault(mapping => string.Equals(mapping.JiraProject, projectName, StringComparison.Ordinal))
        : _mappings.FirstOrDefault(mapping => string.Equals(mapping.AdoProject, projectName, StringComparison.OrdinalIgnoreCase));

    private sealed record Parsed(SourceChange Change, string ProjectName);
}
