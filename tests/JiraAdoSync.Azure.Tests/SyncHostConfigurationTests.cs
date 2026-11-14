namespace JiraAdoSync.Azure.Tests;

public class SyncHostConfigurationTests
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
        {
          "jiraProject": "PROJ",
          "adoProject": "Project",
          "areaPath": "Project",
          "statusMap": { "To Do": "New", "Done": "Closed" }
        }
      ]
    }
    """;

    private static Dictionary<string, string> Settings() => new(StringComparer.Ordinal)
    {
        ["JIRA_API_TOKEN"] = "jira-token-value",
        ["JIRA_WEBHOOK_SECRET"] = "jira-secret-value",
        ["ADO_WEBHOOK_USER"] = "hook-user",
        ["ADO_WEBHOOK_PASSWORD"] = "hook-password-value",
    };

    private static Func<string, string?> Reader(Dictionary<string, string> settings) =>
        name => settings.TryGetValue(name, out var value) ? value : null;

    [Fact]
    public void SecretsAndIdentitiesAreResolvedFromTheSettings()
    {
        var host = SyncHostConfiguration.Load(Config, Reader(Settings()));

        Assert.Equal("jira-token-value", host.JiraApiToken);
        Assert.Equal("jira-secret-value", host.WebhookSecrets.JiraSecret);
        Assert.Equal("hook-user", host.WebhookSecrets.AdoUserName);
        Assert.Equal("hook-password-value", host.WebhookSecrets.AdoPassword);
        Assert.Equal("jira-service-account", host.Identities.JiraServiceAccountId);
        Assert.Equal("sync-identity", host.Identities.AdoServiceIdentity);
        Assert.Null(host.AdoPersonalAccessToken);
    }

    [Fact]
    public void MissingSecretStopsStartupAndNamesTheSettingButNotItsValue()
    {
        var settings = Settings();
        settings.Remove("JIRA_WEBHOOK_SECRET");

        var error = Assert.Throws<InvalidOperationException>(() => SyncHostConfiguration.Load(Config, Reader(settings)));

        Assert.Contains("JIRA_WEBHOOK_SECRET", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("jira-token-value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConfigurationStopsStartupWithEveryError()
    {
        var invalid = Config
            .Replace("https://example.atlassian.net", "http://example.atlassian.net", StringComparison.Ordinal)
            .Replace("\"serviceIdentity\": \"sync-identity\",", string.Empty, StringComparison.Ordinal);

        var error = Assert.Throws<InvalidOperationException>(() => SyncHostConfiguration.Load(invalid, Reader(Settings())));

        Assert.Contains("jira.siteUrl", error.Message, StringComparison.Ordinal);
        Assert.Contains("azureDevOps.serviceIdentity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PatModeNeedsTheSettingThatHoldsThePat()
    {
        var pat = Config
            .Replace("\"managedIdentity\"", "\"pat\"", StringComparison.Ordinal)
            .Replace("\"serviceIdentity\"", "\"patSetting\": \"ADO_PAT\", \"serviceIdentity\"", StringComparison.Ordinal);

        var error = Assert.Throws<InvalidOperationException>(() => SyncHostConfiguration.Load(pat, Reader(Settings())));
        Assert.Contains("ADO_PAT", error.Message, StringComparison.Ordinal);

        var settings = Settings();
        settings["ADO_PAT"] = "pat-value";
        var host = SyncHostConfiguration.Load(pat, Reader(settings));
        Assert.Equal("pat-value", host.AdoPersonalAccessToken);
    }
}
