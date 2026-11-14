using JiraAdoSync.Core.Delivery;

namespace JiraAdoSync.Core.Ports;

/// <summary>Sends messages from the receiver to the processor (ADR 0004). The host implements it with Storage Queue.</summary>
public interface IDeliveryDispatcher
{
    /// <summary>Enqueues the message. Returns false when the write failed, so that the caller can answer with a retryable status.</summary>
    Task<bool> TryEnqueueAsync(DeliveryMessage message, CancellationToken cancellationToken);
}
