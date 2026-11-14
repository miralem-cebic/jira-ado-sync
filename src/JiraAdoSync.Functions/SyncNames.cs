namespace JiraAdoSync.Functions;

/// <summary>The names of the storage resources. They are constants, because a trigger attribute needs a constant.</summary>
public static class SyncNames
{
    /// <summary>The queue between the receivers and the processor (ADR 0004).</summary>
    public const string DeliveryQueue = "sync-deliveries";

    /// <summary>The blob container with the raw bodies of the deliveries.</summary>
    public const string RawDeliveryContainer = "raw-deliveries";

    /// <summary>The table with one row per linked pair (DESIGN.md, section 4).</summary>
    public const string SyncItemsTable = "SyncItems";

    /// <summary>The app setting with the storage connection. The Functions runtime uses the same one for its triggers.</summary>
    public const string StorageConnectionSetting = "AzureWebJobsStorage";

    /// <summary>The app setting with the path of the configuration file. Defaults to config.json next to the app.</summary>
    public const string ConfigPathSetting = "SYNC_CONFIG_PATH";
}
