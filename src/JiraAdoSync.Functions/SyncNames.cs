namespace JiraAdoSync.Functions;

/// <summary>The names of the app settings the host reads. The storage resource names are in <c>StorageNames</c>.</summary>
public static class SyncNames
{
    /// <summary>The app setting with the storage connection. The Functions runtime uses the same one for its triggers.</summary>
    public const string StorageConnectionSetting = "AzureWebJobsStorage";

    /// <summary>The app setting with the path of the configuration file. Defaults to config.json next to the app.</summary>
    public const string ConfigPathSetting = "SYNC_CONFIG_PATH";
}
