using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Sync;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>
/// The host's log lines, one per event. They carry the source, the blob name (a SHA-256, not personal data) and the
/// decision. They never carry the body or the field contents.
/// </summary>
internal static partial class SyncLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Source} delivery answered with {Result}.")]
    public static partial void DeliveryAnswered(ILogger logger, Side source, ReceiveResult result);

    [LoggerMessage(Level = LogLevel.Error, Message = "Receiving a {Source} delivery failed.")]
    public static partial void DeliveryReceiveFailed(ILogger logger, Side source, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivery {Source} item {ItemId} ({BlobName}) processed: {Outcome}.")]
    public static partial void DeliveryProcessed(ILogger logger, Side source, string itemId, string blobName, SyncOutcome outcome);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {Source} ({BlobName}) dropped: {Reason}.")]
    public static partial void DeliveryDropped(ILogger logger, Side source, string blobName, DropReason reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {Source} item {ItemId} ({BlobName}) lost a save race and is retried.")]
    public static partial void DeliveryRetried(ILogger logger, Side source, string itemId, string blobName);
}
