using System.Text.Json;
using System.Text.Json.Serialization;

namespace JiraAdoSync.Core.Config;

/// <summary>Outcome of <see cref="SyncConfigLoader.Parse"/>: either a valid configuration or every error found.</summary>
/// <param name="Config">The validated configuration, or null if any error was found.</param>
/// <param name="Errors">All problems found, each prefixed with the JSON path it refers to. Empty when valid.</param>
public sealed record ConfigLoadResult(SyncConfig? Config, IReadOnlyList<string> Errors)
{
    /// <summary>True if <see cref="Config"/> is set and no error was found.</summary>
    public bool IsValid => Config is not null && Errors.Count == 0;
}

/// <summary>
/// Parses and validates the JSON configuration file.
/// Unknown properties are rejected, so typos fail loudly instead of being silently ignored.
/// Errors are collected rather than thrown, so the user sees all problems at once.
/// </summary>
public static partial class SyncConfigLoader
{
    private static readonly string[] JiraAuthModes = ["apiToken"];
    private static readonly string[] AdoAuthModes = ["managedIdentity", "pat"];
    private static readonly string[] ConflictPolicies = ["lastWriteWins"];
    private static readonly string[] EmptyConfiguration = ["Configuration is empty."];

    /// <summary>Parses <paramref name="json"/> and validates it. Never throws for malformed input.</summary>
    /// <param name="json">Content of the configuration file.</param>
    public static ConfigLoadResult Parse(string json)
    {
        ConfigFile? file;
        try
        {
            file = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.ConfigFile);
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult(null, [$"Invalid configuration JSON: {ex.Message}"]);
        }

        if (file is null)
        {
            return new ConfigLoadResult(null, EmptyConfiguration);
        }

        var errors = new List<string>();

        var jira = ValidateJira(file.Jira, errors);
        var ado = ValidateAzureDevOps(file.AzureDevOps, errors);
        var conflictPolicy = ValidateConflictPolicy(file.ConflictPolicy, errors);
        var features = ToFeatureFlags(file.Features);
        var mappings = ValidateMappings(file.ProjectMappings, features, errors);

        if (errors.Count > 0 || jira is null || ado is null || conflictPolicy is null || mappings is null)
        {
            return new ConfigLoadResult(null, errors);
        }

        var config = new SyncConfig(jira, ado, conflictPolicy, features, mappings);
        return new ConfigLoadResult(config, errors);
    }

    private static JiraSettings? ValidateJira(JiraFile? jira, List<string> errors)
    {
        if (jira is null)
        {
            errors.Add("jira: section is required.");
            return null;
        }

        var valid = true;

        if (!Uri.TryCreate(jira.SiteUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("jira.siteUrl: must be an absolute https URL.");
            valid = false;
        }

        if (!Contains(JiraAuthModes, jira.AuthMode))
        {
            errors.Add($"jira.authMode: must be one of: {string.Join(", ", JiraAuthModes)}.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(jira.ApiTokenSetting))
        {
            errors.Add("jira.apiTokenSetting: is required and must name a setting, not contain the token.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(jira.AccountEmail))
        {
            errors.Add("jira.accountEmail: is required.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(jira.ServiceAccountId))
        {
            errors.Add("jira.serviceAccountId: is required. It is the accountId of the sync's own service account.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(jira.WebhookSecretSetting))
        {
            errors.Add("jira.webhookSecretSetting: is required and must name a setting, not contain the secret.");
            valid = false;
        }

        return valid
            ? new JiraSettings(
                uri!,
                jira.AuthMode!,
                jira.ApiTokenSetting!,
                jira.AccountEmail!,
                jira.ServiceAccountId!,
                jira.WebhookSecretSetting!)
            : null;
    }

    private static AzureDevOpsSettings? ValidateAzureDevOps(AzureDevOpsFile? ado, List<string> errors)
    {
        if (ado is null)
        {
            errors.Add("azureDevOps: section is required.");
            return null;
        }

        var valid = true;

        if (string.IsNullOrWhiteSpace(ado.Organization))
        {
            errors.Add("azureDevOps.organization: is required.");
            valid = false;
        }

        if (!Contains(AdoAuthModes, ado.AuthMode))
        {
            errors.Add($"azureDevOps.authMode: must be one of: {string.Join(", ", AdoAuthModes)}.");
            valid = false;
        }

        if (string.Equals(ado.AuthMode, "pat", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(ado.PatSetting))
        {
            errors.Add("azureDevOps.patSetting: is required when authMode is \"pat\".");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(ado.ServiceIdentity))
        {
            errors.Add("azureDevOps.serviceIdentity: is required. It is the unique name of the sync's own identity.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(ado.WebhookUserSetting))
        {
            errors.Add("azureDevOps.webhookUserSetting: is required and must name a setting, not contain the user name.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(ado.WebhookPasswordSetting))
        {
            errors.Add("azureDevOps.webhookPasswordSetting: is required and must name a setting, not contain the password.");
            valid = false;
        }

        return valid
            ? new AzureDevOpsSettings(
                ado.Organization!,
                ado.AuthMode!,
                ado.PatSetting,
                ado.ServiceIdentity!,
                ado.WebhookUserSetting!,
                ado.WebhookPasswordSetting!)
            : null;
    }

    private static string? ValidateConflictPolicy(string? policy, List<string> errors)
    {
        if (!Contains(ConflictPolicies, policy))
        {
            errors.Add($"conflictPolicy: must be one of: {string.Join(", ", ConflictPolicies)}.");
            return null;
        }

        return policy;
    }

    private static FeatureFlags ToFeatureFlags(FeaturesFile? features)
    {
        // Missing fields default to true, matching the JSON schema.
        return new FeatureFlags(
            Title: features?.Title ?? true,
            Description: features?.Description ?? true,
            Status: features?.Status ?? true,
            Assignee: features?.Assignee ?? true);
    }

    private static List<ProjectMapping>? ValidateMappings(
        List<ProjectMappingFile>? files,
        FeatureFlags features,
        List<string> errors)
    {
        if (files is null || files.Count == 0)
        {
            errors.Add("projectMappings: at least one mapping is required.");
            return null;
        }

        var result = new List<ProjectMapping>();
        var seenJiraProjects = new HashSet<string>(StringComparer.Ordinal);
        var valid = true;

        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var path = $"projectMappings[{i}]";

            if (string.IsNullOrWhiteSpace(file.JiraProject)
                || string.IsNullOrWhiteSpace(file.AdoProject)
                || string.IsNullOrWhiteSpace(file.AreaPath))
            {
                errors.Add($"{path}: jiraProject, adoProject and areaPath are required.");
                valid = false;
                continue;
            }

            if (file.StatusMap is null || file.StatusMap.Count == 0)
            {
                errors.Add($"{path}.statusMap: at least one status mapping is required.");
                valid = false;
                continue;
            }

            foreach (var (jiraStatus, adoState) in file.StatusMap)
            {
                if (string.IsNullOrWhiteSpace(adoState))
                {
                    errors.Add($"{path}.statusMap.{jiraStatus}: the ADO state must not be empty.");
                    valid = false;
                }
            }

            if (!seenJiraProjects.Add(file.JiraProject))
            {
                errors.Add($"{path}.jiraProject: '{file.JiraProject}' is mapped more than once.");
                valid = false;
                continue;
            }

            var mapping = new ProjectMapping
            {
                Id = file.JiraProject,
                JiraProject = file.JiraProject,
                AdoProject = file.AdoProject,
                AreaPath = file.AreaPath,
                StatusMap = new Dictionary<string, string>(file.StatusMap, StringComparer.Ordinal),
                Features = features,
            };

            var mappingErrors = mapping.Validate();
            foreach (var error in mappingErrors)
            {
                errors.Add($"{path}: {error}");
            }

            if (mappingErrors.Count > 0)
            {
                valid = false;
            }

            result.Add(mapping);
        }

        return valid ? result : null;
    }

    private static bool Contains(IEnumerable<string> allowed, string? value) =>
        value is not null && allowed.Any(a => string.Equals(a, value, StringComparison.Ordinal));

    // DTOs: mirror the JSON file. Properties are nullable so missing values produce clear errors.
    // They are private: only the loader sees the raw file shape; callers get the validated SyncConfig.

    // DTO properties are PascalCase in C#; the JSON file uses camelCase, as in the schema.
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = false)]
    [JsonSerializable(typeof(ConfigFile))]
    private sealed partial class ConfigJsonContext : JsonSerializerContext;

    private sealed class ConfigFile
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        public JiraFile? Jira { get; set; }

        public AzureDevOpsFile? AzureDevOps { get; set; }

        public string? ConflictPolicy { get; set; }

        public FeaturesFile? Features { get; set; }

        public List<ProjectMappingFile>? ProjectMappings { get; set; }
    }

    private sealed class JiraFile
    {
        public string? SiteUrl { get; set; }

        public string? AuthMode { get; set; }

        public string? ApiTokenSetting { get; set; }

        public string? AccountEmail { get; set; }

        public string? ServiceAccountId { get; set; }

        public string? WebhookSecretSetting { get; set; }
    }

    private sealed class AzureDevOpsFile
    {
        public string? Organization { get; set; }

        public string? AuthMode { get; set; }

        public string? PatSetting { get; set; }

        public string? ServiceIdentity { get; set; }

        public string? WebhookUserSetting { get; set; }

        public string? WebhookPasswordSetting { get; set; }
    }

    private sealed class FeaturesFile
    {
        public bool? Title { get; set; }

        public bool? Description { get; set; }

        public bool? Status { get; set; }

        public bool? Assignee { get; set; }
    }

    private sealed class ProjectMappingFile
    {
        public string? JiraProject { get; set; }

        public string? AdoProject { get; set; }

        public string? AreaPath { get; set; }

        public Dictionary<string, string>? StatusMap { get; set; }
    }
}
