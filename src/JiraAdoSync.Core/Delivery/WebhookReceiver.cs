using System.Text.Json;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;
using JiraAdoSync.Core.Webhooks;

namespace JiraAdoSync.Core.Delivery;

/// <summary>
/// The receiving half of the webhook path (ADR 0004): authenticate (ADR 0006), check that the body is JSON, store the raw
/// body, enqueue a message that names it, and answer. It does not parse the event or call Jira or ADO, so a rate limit or a
/// slow call cannot make the vendor retry or put the subscription on probation.
/// </summary>
public sealed class WebhookReceiver
{
    private readonly WebhookSecrets _secrets;
    private readonly IRawDeliveryStore _store;
    private readonly IDeliveryDispatcher _queue;

    /// <param name="secrets">The credentials deliveries are checked against.</param>
    /// <param name="store">Keeps the raw bodies.</param>
    /// <param name="queue">Sends the messages to the processor.</param>
    /// <exception cref="ArgumentException">A secret is null or empty. That is a configuration error.</exception>
    public WebhookReceiver(WebhookSecrets secrets, IRawDeliveryStore store, IDeliveryDispatcher queue)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentException.ThrowIfNullOrEmpty(secrets.JiraSecret);
        ArgumentException.ThrowIfNullOrEmpty(secrets.AdoUserName);
        ArgumentException.ThrowIfNullOrEmpty(secrets.AdoPassword);

        _secrets = secrets;
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>Receives one delivery. Safe to call again for a redelivery: the body keeps one name and the processor is idempotent.</summary>
    /// <param name="request">The delivery and the headers the receiver needs.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task<ReceiveResult> ReceiveAsync(WebhookRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsAuthentic(request))
        {
            return ReceiveResult.Unauthorized;
        }

        if (!IsJson(request.Body))
        {
            return ReceiveResult.NotJson;
        }

        var blobName = DeliveryKey.For(request.Source, request.Body.Span);

        // The body is stored before its message is enqueued, so the processor always finds it.
        if (!await _store.TryStoreAsync(blobName, request.Body, cancellationToken).ConfigureAwait(false))
        {
            return ReceiveResult.Unavailable;
        }

        var message = new DeliveryMessage(request.Source, blobName);
        if (!await _queue.TryEnqueueAsync(message, cancellationToken).ConfigureAwait(false))
        {
            return ReceiveResult.Unavailable;
        }

        return ReceiveResult.Accepted;
    }

    private bool IsAuthentic(WebhookRequest request) => request.Source switch
    {
        Side.Jira => WebhookAuthenticator.IsValidJiraSignature(request.Body.Span, request.SignatureHeader, _secrets.JiraSecret),
        Side.Ado => WebhookAuthenticator.IsValidBasicAuthorization(
            request.AuthorizationHeader,
            _secrets.AdoUserName,
            _secrets.AdoPassword),
        _ => false,
    };

    private static bool IsJson(ReadOnlyMemory<byte> body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind is not JsonValueKind.Undefined;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
