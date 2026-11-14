using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Ports;
using JiraAdoSync.Core.Sync;
using Microsoft.Extensions.DependencyInjection;

namespace JiraAdoSync.Functions.Tests;

public class SyncHostRegistrationTests
{
    private const string Config = """
    {
      "jira": {
        "siteUrl": "https://example.atlassian.net",
        "authMode": "apiToken",
        "apiTokenSetting": "JIRA_API_TOKEN",
        "accountEmail": "sync@example.test",
        "serviceAccountId": "jira-service-account",
        "webhookSecretSetting": "JIRA_WEBHOOK_SECRET"
      },
      "azureDevOps": {
        "organization": "example-org",
        "authMode": "managedIdentity",
        "serviceIdentity": "sync-identity",
        "webhookUserSetting": "ADO_WEBHOOK_USER",
        "webhookPasswordSetting": "ADO_WEBHOOK_PASSWORD"
      },
      "conflictPolicy": "lastWriteWins",
      "projectMappings": [
        { "jiraProject": "PROJ", "adoProject": "Project", "areaPath": "Project", "statusMap": { "To Do": "New", "Done": "Closed" } }
      ]
    }
    """;

    private static string? Read(string name) => name switch
    {
        "JIRA_API_TOKEN" => "jira-token-value",
        "JIRA_WEBHOOK_SECRET" => "jira-secret-value",
        "ADO_WEBHOOK_USER" => "hook-user",
        "ADO_WEBHOOK_PASSWORD" => "hook-password-value",
        _ => null,
    };

    private static ServiceCollection Registered()
    {
        var services = new ServiceCollection();
        SyncHostRegistration.Register(services, Config, Read, "UseDevelopmentStorage=true");
        return services;
    }

    private static ServiceLifetime LifetimeOf<T>(IServiceCollection services) =>
        services.Single(descriptor => descriptor.ServiceType == typeof(T)).Lifetime;

    [Fact]
    public void ClientsAndProcessorsAreNotSingletons()
    {
        // A singleton would keep one HttpClient for the life of the process, so the handler lifetime of the factory would not apply.
        var services = Registered();

        Assert.Equal(ServiceLifetime.Transient, LifetimeOf<IJiraClient>(services));
        Assert.Equal(ServiceLifetime.Transient, LifetimeOf<IAdoClient>(services));
        Assert.Equal(ServiceLifetime.Transient, LifetimeOf<SyncProcessor>(services));
        Assert.Equal(ServiceLifetime.Transient, LifetimeOf<DeliveryProcessor>(services));
    }

    [Fact]
    public void TheProcessingGraphResolvesWithoutCallingAnyExternalSystem()
    {
        using var provider = Registered().BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<WebhookReceiver>());
        Assert.NotNull(provider.GetRequiredService<DeliveryProcessor>());
        Assert.NotNull(provider.GetRequiredService<IJiraClient>());
        Assert.NotNull(provider.GetRequiredService<IAdoClient>());
    }
}
