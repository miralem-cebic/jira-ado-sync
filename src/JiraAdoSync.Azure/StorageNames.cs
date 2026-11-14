namespace JiraAdoSync.Azure;

/// <summary>The names of the storage resources. They are constants, because a trigger attribute needs a constant.</summary>
public static class StorageNames
{
    /// <summary>The blob container with the raw bodies of the deliveries.</summary>
    public const string RawDeliveryContainer = "raw-deliveries";

    /// <summary>The queue between the receivers and the processor (ADR 0004).</summary>
    public const string DeliveryQueue = "sync-deliveries";

    /// <summary>The table with one row per linked pair (DESIGN.md, section 4).</summary>
    public const string SyncItemsTable = "SyncItems";
}
