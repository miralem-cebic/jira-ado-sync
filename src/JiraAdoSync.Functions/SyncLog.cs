using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>
/// The host's log lines, one per event. They carry the source, the item (a Jira key or an ADO id), the blob name (a SHA-256)
/// and the decision. They never carry the body, a header or a secret.
/// </summary>
internal static partial class SyncLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Source} delivery answered with {Result}.")]
    public static partial void DeliveryAnswered(ILogger logger, Side source, ReceiveResult result);

    [LoggerMessage(Level = LogLevel.Error, Message = "Receiving a {Source} delivery failed. It is answered with 503.")]
    public static partial void DeliveryReceiveFailed(ILogger logger, Side source, Exception? exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Source} delivery is larger than {Limit} bytes. It is answered with 413.")]
    public static partial void DeliveryTooLarge(ILogger logger, Side source, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivery {Source} item {ItemId} ({BlobName}) processed: {Outcome}.")]
    public static partial void DeliveryProcessed(ILogger logger, Side source, string itemId, string blobName, SyncOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {Source} ({BlobName}) dropped: {Reason}.")]
    public static partial void DeliveryDropped(ILogger logger, Side source, string blobName, DropReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {Source} item {ItemId} ({BlobName}) lost a save race and is retried.")]
    public static partial void DeliveryRetried(ILogger logger, Side source, string itemId, string blobName);
}
