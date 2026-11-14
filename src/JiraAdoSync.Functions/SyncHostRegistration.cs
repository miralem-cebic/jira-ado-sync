using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using JiraAdoSync.Adapters;
using JiraAdoSync.Azure;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Ports;
using JiraAdoSync.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace JiraAdoSync.Functions;

/// <summary>
/// Builds the object graph of the host (DESIGN.md, section 3). The configuration is read and validated here, so a bad
/// configuration or a missing secret stops the host at startup, before the first delivery arrives.
/// </summary>
public static class SyncHostRegistration
{
    // The Azure DevOps resource: the managed identity asks Entra ID for a token for it.
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    /// <summary>Reads the configuration file and the storage connection from the environment, then registers the host.</summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configPath = Environment.GetEnvironmentVariable(SyncNames.ConfigPathSetting)
            ?? Path.Combine(AppContext.BaseDirectory, "config.json");
        var storage = Environment.GetEnvironmentVariable(SyncNames.StorageConnectionSetting)
            ?? throw new InvalidOperationException($"The app setting {SyncNames.StorageConnectionSetting} is not set.");

        Register(services, File.ReadAllText(configPath), Environment.GetEnvironmentVariable, storage);
    }

    /// <summary>
    /// Registers the core, the adapters and the Azure resources. Clients and processors are transient. A singleton would
    /// keep one HttpClient for the life of the process, so the handler lifetime of IHttpClientFactory would not apply.
    /// </summary>
    /// <param name="services">The service collection to fill.</param>
    /// <param name="configJson">The content of the configuration file.</param>
    /// <param name="readSetting">Reads an app setting or environment variable by name.</param>
    /// <param name="storageConnection">The storage connection string.</param>
    public static void Register(IServiceCollection services, string configJson, Func<string, string?> readSetting, string storageConnection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configJson);
        ArgumentNullException.ThrowIfNull(readSetting);
        ArgumentException.ThrowIfNullOrEmpty(storageConnection);

        var settings = SyncHostConfiguration.Load(configJson, readSetting);
        var storage = storageConnection;

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(settings.WebhookSecrets);
        services.AddSingleton(settings.Identities);
        services.AddSingleton(settings.Config.ProjectMappings);
        services.AddSingleton<IRawDeliveryStore>(_ => new BlobRawDeliveryStore(
            new BlobContainerClient(storage, StorageNames.RawDeliveryContainer)));
        services.AddSingleton<IDeliveryDispatcher>(_ => new StorageQueueDispatcher(
            StorageClients.CreateDeliveryQueue(storage, StorageNames.DeliveryQueue)));
        services.AddSingleton<ISyncStateStore>(_ => new TableSyncStateStore(
            new TableClient(storage, StorageNames.SyncItemsTable)));
        services.AddSingleton<WebhookReceiver>();
        services.AddTransient<SyncProcessor>();
        services.AddTransient<DeliveryProcessor>();
        services.AddHostedService<StorageResourcesInitializer>();

        services.AddHttpClient<IJiraClient, JiraClient>(client => client.BaseAddress = settings.Config.Jira.SiteUrl)
            .AddHttpMessageHandler(() => new RetryingHandler(new RetryOptions(), TimeProvider.System, Random.Shared))
            .AddHttpMessageHandler(() => new BasicAuthorizationHandler(settings.Config.Jira.AccountEmail, settings.JiraApiToken));

        var adoBase = new Uri("https://dev.azure.com/" + Uri.EscapeDataString(settings.Config.AzureDevOps.Organization) + "/");
        var credential = new DefaultAzureCredential();
        services.AddHttpClient<IAdoClient, AdoClient>(client => client.BaseAddress = adoBase)
            .AddHttpMessageHandler(() => new RetryingHandler(new RetryOptions(), TimeProvider.System, Random.Shared))
            .AddHttpMessageHandler(() => settings.AdoPersonalAccessToken is null
                ? new BearerAuthorizationHandler(credential, AzureDevOpsScope)
                : new BasicAuthorizationHandler(string.Empty, settings.AdoPersonalAccessToken));
    }
}
