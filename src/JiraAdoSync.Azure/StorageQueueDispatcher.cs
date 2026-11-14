using Azure;
using Azure.Storage.Queues;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Azure;

/// <summary>
/// Sends the delivery messages to the Storage Queue the processor is triggered by (ADR 0004). The client must come from
/// <see cref="StorageClients.CreateDeliveryQueue"/>, which sets the Base64 encoding the trigger expects.
/// </summary>
public sealed class StorageQueueDispatcher(QueueClient queue) : IDeliveryDispatcher
{
    private readonly QueueClient _queue = queue ?? throw new ArgumentNullException(nameof(queue));

    /// <inheritdoc />
    public async Task<bool> TryEnqueueAsync(DeliveryMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            await _queue.SendMessageAsync(DeliveryMessageJson.Serialize(message), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }
}
