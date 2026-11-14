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

    /// <summary>Registers the core, the adapters and the Azure resources.</summary>
    public static void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var configPath = Environment.GetEnvironmentVariable(SyncNames.ConfigPathSetting)
            ?? Path.Combine(AppContext.BaseDirectory, "config.json");
        var settings = SyncHostConfiguration.Load(File.ReadAllText(configPath), Environment.GetEnvironmentVariable);

        var storage = Environment.GetEnvironmentVariable(SyncNames.StorageConnectionSetting)
            ?? throw new InvalidOperationException($"The app setting {SyncNames.StorageConnectionSetting} is not set.");

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(settings.WebhookSecrets);
        services.AddSingleton(settings.Identities);
        services.AddSingleton(settings.Config.ProjectMappings);
        services.AddSingleton<IRawDeliveryStore>(_ => new BlobRawDeliveryStore(
            new BlobContainerClient(storage, SyncNames.RawDeliveryContainer)));
        services.AddSingleton<IDeliveryDispatcher>(_ => new StorageQueueDispatcher(
            StorageClients.CreateDeliveryQueue(storage, SyncNames.DeliveryQueue)));
        services.AddSingleton<ISyncStateStore>(_ => new TableSyncStateStore(
            new TableClient(storage, SyncNames.SyncItemsTable)));
        services.AddSingleton<WebhookReceiver>();
        services.AddSingleton<SyncProcessor>();
        services.AddSingleton<DeliveryProcessor>();
        services.AddHostedService<StorageResourcesInitializer>();

        services.AddHttpClient<JiraClient>(client => client.BaseAddress = settings.Config.Jira.SiteUrl)
            .AddHttpMessageHandler(() => new RetryingHandler(new RetryOptions(), TimeProvider.System, Random.Shared))
            .AddHttpMessageHandler(() => new BasicAuthorizationHandler(settings.Config.Jira.AccountEmail, settings.JiraApiToken));
        services.AddSingleton<IJiraClient>(provider => provider.GetRequiredService<JiraClient>());

        var adoBase = new Uri("https://dev.azure.com/" + Uri.EscapeDataString(settings.Config.AzureDevOps.Organization) + "/");
        var credential = new DefaultAzureCredential();
        services.AddHttpClient<AdoClient>(client => client.BaseAddress = adoBase)
            .AddHttpMessageHandler(() => new RetryingHandler(new RetryOptions(), TimeProvider.System, Random.Shared))
            .AddHttpMessageHandler(() => settings.AdoPersonalAccessToken is null
                ? new BearerAuthorizationHandler(credential, AzureDevOpsScope)
                : new BasicAuthorizationHandler(string.Empty, settings.AdoPersonalAccessToken));
        services.AddSingleton<IAdoClient>(provider => provider.GetRequiredService<AdoClient>());
    }
}
