namespace JiraAdoSync.Azure.Tests;

public class SyncHostSettingsTests
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
        { "jiraProject": "PROJ", "adoProject": "Project", "areaPath": "Project", "statusMap": { "To Do": "New" } }
      ]
    }
    """;

    [Fact]
    public void ToStringHidesEveryValue()
    {
        var settings = SyncHostConfiguration.Load(Config, name => name switch
        {
            "JIRA_API_TOKEN" => "jira-token-value",
            "JIRA_WEBHOOK_SECRET" => "jira-secret-value",
            "ADO_WEBHOOK_USER" => "hook-user-value",
            "ADO_WEBHOOK_PASSWORD" => "hook-password-value",
            _ => null,
        });

        var text = settings.ToString();

        Assert.DoesNotContain("jira-token-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("jira-secret-value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("hook-password-value", text, StringComparison.Ordinal);
    }
}
