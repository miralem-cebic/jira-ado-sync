using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Delivery;

namespace JiraAdoSync.Azure;

/// <summary>Everything the host needs at startup: the validated configuration and the values of the secrets it names.</summary>
/// <param name="Config">The validated configuration.</param>
/// <param name="JiraApiToken">The Jira API token, from the setting named by <c>jira.apiTokenSetting</c>.</param>
/// <param name="WebhookSecrets">The credentials the webhook receiver checks deliveries against.</param>
/// <param name="Identities">The sync's own identities, for the echo check.</param>
/// <param name="AdoPersonalAccessToken">The PAT for local testing, or null unless <c>azureDevOps.authMode</c> is <c>pat</c>.</param>
public sealed record SyncHostSettings(
    SyncConfig Config,
    string JiraApiToken,
    WebhookSecrets WebhookSecrets,
    SyncIdentities Identities,
    string? AdoPersonalAccessToken);

/// <summary>
/// Validates the configuration and reads the secrets it names. Startup stops with one error that lists every problem.
/// The error names the settings that are missing, but never prints a value.
/// </summary>
public static class SyncHostConfiguration
{
    /// <param name="configJson">The content of the configuration file.</param>
    /// <param name="readSetting">Reads an app setting or environment variable by name. Returns null when it is not set.</param>
    /// <exception cref="InvalidOperationException">The configuration is invalid, or a setting it names is not set.</exception>
    public static SyncHostSettings Load(string configJson, Func<string, string?> readSetting)
    {
        ArgumentNullException.ThrowIfNull(configJson);
        ArgumentNullException.ThrowIfNull(readSetting);

        var result = SyncConfigLoader.Parse(configJson);
        if (!result.IsValid || result.Config is null)
        {
            throw new InvalidOperationException("The configuration is invalid: " + string.Join(" ", result.Errors));
        }

        var config = result.Config;
        var names = new List<string>
        {
            config.Jira.ApiTokenSetting,
            config.Jira.WebhookSecretSetting,
            config.AzureDevOps.WebhookUserSetting,
            config.AzureDevOps.WebhookPasswordSetting,
        };
        if (config.AzureDevOps.PatSetting is not null)
        {
            names.Add(config.AzureDevOps.PatSetting);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var missing = new List<string>();
        foreach (var name in names)
        {
            var value = readSetting(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(name);
            }
            else
            {
                values[name] = value;
            }
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "These settings are not set: " + string.Join(", ", missing) + ". Set them as app settings or environment variables.");
        }

        return new SyncHostSettings(
            config,
            values[config.Jira.ApiTokenSetting],
            new WebhookSecrets(
                values[config.Jira.WebhookSecretSetting],
                values[config.AzureDevOps.WebhookUserSetting],
                values[config.AzureDevOps.WebhookPasswordSetting]),
            new SyncIdentities(config.Jira.ServiceAccountId, config.AzureDevOps.ServiceIdentity),
            config.AzureDevOps.PatSetting is null ? null : values[config.AzureDevOps.PatSetting]);
    }
}
