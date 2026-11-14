using JiraAdoSync.Core.Config;

namespace JiraAdoSync.Core.Tests;

public class SyncConfigLoaderTests
{
    private const string ValidJson = """
    {
      "$schema": "./config.schema.json",
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
      "features": { "title": true, "description": false },
      "projectMappings": [
        {
          "jiraProject": "PROJ",
          "adoProject": "Project",
          "areaPath": "Project\\Team A",
          "statusMap": { "To Do": "New", "In Progress": "Active", "Done": "Closed" }
        }
      ]
    }
    """;

    [Fact]
    public void ShippedExampleConfigurationIsValid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config.example.json");

        var result = SyncConfigLoader.Parse(File.ReadAllText(path));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void ValidConfigurationIsLoaded()
    {
        var result = SyncConfigLoader.Parse(ValidJson);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var config = Assert.IsType<SyncConfig>(result.Config);
        Assert.Equal(new Uri("https://example.atlassian.net"), config.Jira.SiteUrl);
        Assert.Equal("JIRA_API_TOKEN", config.Jira.ApiTokenSetting);
        Assert.Equal("example-org", config.AzureDevOps.Organization);
        Assert.Null(config.AzureDevOps.PatSetting);
        Assert.Single(config.ProjectMappings);
        Assert.Equal("PROJ", config.ProjectMappings[0].JiraProject);
        Assert.Equal("Active", config.ProjectMappings[0].ToAdoState("In Progress"));
    }

    [Fact]
    public void SyncIdentityAndWebhookSettingNamesAreLoaded()
    {
        var config = Assert.IsType<SyncConfig>(SyncConfigLoader.Parse(ValidJson).Config);

        Assert.Equal("sync@example.test", config.Jira.AccountEmail);
        Assert.Equal("jira-service-account", config.Jira.ServiceAccountId);
        Assert.Equal("JIRA_WEBHOOK_SECRET", config.Jira.WebhookSecretSetting);
        Assert.Equal("sync-identity", config.AzureDevOps.ServiceIdentity);
        Assert.Equal("ADO_WEBHOOK_USER", config.AzureDevOps.WebhookUserSetting);
        Assert.Equal("ADO_WEBHOOK_PASSWORD", config.AzureDevOps.WebhookPasswordSetting);
    }

    [Fact]
    public void BlankWebhookSecretSettingNameIsRejected()
    {
        var json = ValidJson.Replace("\"webhookSecretSetting\": \"JIRA_WEBHOOK_SECRET\"", "\"webhookSecretSetting\": \"  \"", StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("jira.webhookSecretSetting", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void MissingFeatureFlagsDefaultToTrueAndExplicitFalseIsKept()
    {
        var config = Assert.IsType<SyncConfig>(SyncConfigLoader.Parse(ValidJson).Config);

        Assert.True(config.Features.Title);
        Assert.False(config.Features.Description);
        Assert.True(config.Features.Status);
        Assert.True(config.Features.Assignee);
    }

    [Fact]
    public void UnknownPropertyIsRejectedSoTyposDoNotPassSilently()
    {
        var json = ValidJson.Replace("\"conflictPolicy\"", "\"conflictPolicyy\"", StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid configuration JSON", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NonHttpsSiteUrlIsRejected()
    {
        var json = ValidJson.Replace("https://example.atlassian.net", "http://example.atlassian.net", StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("jira.siteUrl", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void PatAuthRequiresPatSetting()
    {
        var json = ValidJson.Replace("\"managedIdentity\"", "\"pat\"", StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("azureDevOps.patSetting", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateJiraProjectIsRejected()
    {
        var mapping = """
        {
          "jiraProject": "PROJ",
          "adoProject": "Project",
          "areaPath": "Project",
          "statusMap": { "To Do": "New" }
        }
        """;
        var json = ValidJson.Replace(
            "\"projectMappings\": [",
            "\"projectMappings\": [" + mapping + ",",
            StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("mapped more than once", string.Join(" ", result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void NonOneToOneStatusMapIsReportedThroughTheLoader()
    {
        var json = ValidJson.Replace(
            "\"Done\": \"Closed\"",
            "\"Done\": \"Active\"",
            StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("one-to-one", string.Join(" ", result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedConflictPolicyIsRejected()
    {
        var json = ValidJson.Replace("\"lastWriteWins\"", "\"sourceOfTruthPerField\"", StringComparison.Ordinal);

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("conflictPolicy", result.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyProjectMappingsAreRejected()
    {
        var json = """
        {
          "jira": {
            "siteUrl": "https://example.atlassian.net", "authMode": "apiToken", "apiTokenSetting": "JIRA_API_TOKEN",
            "accountEmail": "sync@example.test", "serviceAccountId": "jira-service-account",
            "webhookSecretSetting": "JIRA_WEBHOOK_SECRET"
          },
          "azureDevOps": {
            "organization": "example-org", "authMode": "managedIdentity", "serviceIdentity": "sync-identity",
            "webhookUserSetting": "ADO_WEBHOOK_USER", "webhookPasswordSetting": "ADO_WEBHOOK_PASSWORD"
          },
          "conflictPolicy": "lastWriteWins",
          "projectMappings": []
        }
        """;

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("at least one mapping", result.Errors[0], StringComparison.Ordinal);
    }
}
