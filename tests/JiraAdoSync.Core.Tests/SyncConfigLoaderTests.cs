using JiraAdoSync.Core.Config;
using Xunit;

namespace JiraAdoSync.Core.Tests;

public class SyncConfigLoaderTests
{
    private const string ValidJson = """
    {
      "$schema": "./config.schema.json",
      "jira": {
        "siteUrl": "https://example.atlassian.net",
        "authMode": "apiToken",
        "apiTokenSetting": "JIRA_API_TOKEN"
      },
      "azureDevOps": {
        "organization": "example-org",
        "authMode": "managedIdentity"
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
    public void Shipped_example_configuration_is_valid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config.example.json");

        var result = SyncConfigLoader.Parse(File.ReadAllText(path));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Valid_configuration_is_loaded()
    {
        var result = SyncConfigLoader.Parse(ValidJson);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var config = Assert.IsType<SyncConfig>(result.Config);
        Assert.Equal("https://example.atlassian.net", config.Jira.SiteUrl);
        Assert.Equal("JIRA_API_TOKEN", config.Jira.ApiTokenSetting);
        Assert.Equal("example-org", config.AzureDevOps.Organization);
        Assert.Null(config.AzureDevOps.PatSetting);
        Assert.Single(config.ProjectMappings);
        Assert.Equal("PROJ", config.ProjectMappings[0].JiraProject);
        Assert.Equal("Active", config.ProjectMappings[0].ToAdoState("In Progress"));
    }

    [Fact]
    public void Missing_feature_flags_default_to_true_and_explicit_false_is_kept()
    {
        var config = Assert.IsType<SyncConfig>(SyncConfigLoader.Parse(ValidJson).Config);

        Assert.True(config.Features.Title);
        Assert.False(config.Features.Description);
        Assert.True(config.Features.Status);
        Assert.True(config.Features.Assignee);
    }

    [Fact]
    public void Unknown_property_is_rejected_so_typos_do_not_pass_silently()
    {
        var json = ValidJson.Replace("\"conflictPolicy\"", "\"conflictPolicyy\"");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid configuration JSON", result.Errors[0]);
    }

    [Fact]
    public void Non_https_site_url_is_rejected()
    {
        var json = ValidJson.Replace("https://example.atlassian.net", "http://example.atlassian.net");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("jira.siteUrl", result.Errors[0]);
    }

    [Fact]
    public void Pat_auth_requires_pat_setting()
    {
        var json = ValidJson.Replace("\"managedIdentity\"", "\"pat\"");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("azureDevOps.patSetting", result.Errors[0]);
    }

    [Fact]
    public void Duplicate_jira_project_is_rejected()
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
            "\"projectMappings\": [" + mapping + ",");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("mapped more than once", string.Join(" ", result.Errors));
    }

    [Fact]
    public void Non_one_to_one_status_map_is_reported_through_the_loader()
    {
        var json = ValidJson.Replace(
            "\"Done\": \"Closed\"",
            "\"Done\": \"Active\"");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("one-to-one", string.Join(" ", result.Errors));
    }

    [Fact]
    public void Unsupported_conflict_policy_is_rejected()
    {
        var json = ValidJson.Replace("\"lastWriteWins\"", "\"sourceOfTruthPerField\"");

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("conflictPolicy", result.Errors[0]);
    }

    [Fact]
    public void Empty_project_mappings_are_rejected()
    {
        var json = """
        {
          "jira": { "siteUrl": "https://example.atlassian.net", "authMode": "apiToken", "apiTokenSetting": "JIRA_API_TOKEN" },
          "azureDevOps": { "organization": "example-org", "authMode": "managedIdentity" },
          "conflictPolicy": "lastWriteWins",
          "projectMappings": []
        }
        """;

        var result = SyncConfigLoader.Parse(json);

        Assert.False(result.IsValid);
        Assert.Contains("at least one mapping", result.Errors[0]);
    }
}
