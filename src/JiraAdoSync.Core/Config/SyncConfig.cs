namespace JiraAdoSync.Core.Config;

/// <summary>Validated, immutable configuration. Produced by <see cref="SyncConfigLoader"/>.</summary>
/// <param name="Jira">Jira Cloud connection settings.</param>
/// <param name="AzureDevOps">Azure DevOps connection settings.</param>
/// <param name="ConflictPolicy">How concurrent changes on both sides are resolved. Currently always "lastWriteWins".</param>
/// <param name="Features">Which fields take part in the sync, for every mapping.</param>
/// <param name="ProjectMappings">The Jira project to Azure DevOps project pairs that are kept in sync.</param>
public sealed record SyncConfig(
    JiraSettings Jira,
    AzureDevOpsSettings AzureDevOps,
    string ConflictPolicy,
    FeatureFlags Features,
    IReadOnlyList<ProjectMapping> ProjectMappings);

/// <summary>Jira Cloud connection settings.</summary>
/// <param name="SiteUrl">Base URL of the Jira Cloud site (always https).</param>
/// <param name="AuthMode">Currently always "apiToken".</param>
/// <param name="ApiTokenSetting">Name of the setting that holds the token. Never the token itself.</param>
public sealed record JiraSettings(Uri SiteUrl, string AuthMode, string ApiTokenSetting);

/// <summary>Azure DevOps connection settings.</summary>
/// <param name="Organization">Azure DevOps organization name.</param>
/// <param name="AuthMode">"managedIdentity" or "pat".</param>
/// <param name="PatSetting">Name of the setting that holds the PAT. Null unless AuthMode is "pat".</param>
public sealed record AzureDevOpsSettings(string Organization, string AuthMode, string? PatSetting);
