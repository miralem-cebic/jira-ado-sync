using JiraAdoSync.Core.Delivery;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>Processes the queued deliveries (ADR 0004). A throw makes the queue retry the message; the queue then moves it to the poison queue.</summary>
public sealed class DeliveryProcessorFunction(DeliveryProcessor processor, ILogger<DeliveryProcessorFunction> logger)
{
    /// <summary>Processes one delivery message.</summary>
    [Function("ProcessDelivery")]
    public async Task RunAsync(
        [QueueTrigger(SyncNames.DeliveryQueue, Connection = SyncNames.StorageConnectionSetting)] string message,
        CancellationToken cancellationToken)
    {
        var delivery = DeliveryMessageJson.Deserialize(message);
        var outcome = await processor.ProcessAsync(delivery, cancellationToken).ConfigureAwait(false);

        switch (outcome)
        {
            case DeliveryOutcome.Processed processed:
                SyncLog.DeliveryProcessed(logger, delivery.Source, processed.SourceItemId, delivery.BlobName, processed.Outcome);
                break;

            case DeliveryOutcome.Dropped dropped:
                SyncLog.DeliveryDropped(logger, delivery.Source, delivery.BlobName, dropped.Reason);
                break;

            case DeliveryOutcome.RetryLater retry:
                SyncLog.DeliveryRetried(logger, delivery.Source, retry.SourceItemId, delivery.BlobName);
                throw new InvalidOperationException("Another writer saved the pair first. The queue retries the delivery.");
        }
    }
}
