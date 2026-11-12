namespace JiraAdoSync.Core.Config;

/// <summary>Validated, immutable configuration. Produced by <see cref="SyncConfigLoader"/>.</summary>
public sealed record SyncConfig(
    JiraSettings Jira,
    AzureDevOpsSettings AzureDevOps,
    string ConflictPolicy,
    FeatureFlags Features,
    IReadOnlyList<ProjectMapping> ProjectMappings);

/// <param name="SiteUrl">Base URL of the Jira Cloud site.</param>
/// <param name="AuthMode">Currently always "apiToken".</param>
/// <param name="ApiTokenSetting">Name of the setting that holds the token. Never the token itself.</param>
public sealed record JiraSettings(string SiteUrl, string AuthMode, string ApiTokenSetting);

/// <param name="Organization">Azure DevOps organization name.</param>
/// <param name="AuthMode">"managedIdentity" or "pat".</param>
/// <param name="PatSetting">Name of the setting that holds the PAT. Null unless AuthMode is "pat".</param>
public sealed record AzureDevOpsSettings(string Organization, string AuthMode, string? PatSetting);
