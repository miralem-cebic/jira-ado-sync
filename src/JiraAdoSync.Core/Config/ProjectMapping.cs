namespace JiraAdoSync.Core.Config;

/// <summary>
/// Maps one Jira project to one Azure DevOps project and area path.
/// Status mapping must be one-to-one so that ADO states can be translated back to Jira statuses without loss.
/// </summary>
public sealed class ProjectMapping
{
    /// <summary>Identifier of the mapping, e.g. the partition key of the stored state. Currently the Jira project key.</summary>
    public required string Id { get; init; }

    /// <summary>Jira project key, e.g. <c>PROJ</c>.</summary>
    public required string JiraProject { get; init; }

    /// <summary>Azure DevOps project name.</summary>
    public required string AdoProject { get; init; }

    /// <summary>Azure DevOps area path that new work items are created in.</summary>
    public required string AreaPath { get; init; }

    /// <summary>Jira status name → ADO state name.</summary>
    public required IReadOnlyDictionary<string, string> StatusMap { get; init; }

    /// <summary>Which fields take part in the sync for this mapping.</summary>
    public FeatureFlags Features { get; init; } = new();

    /// <summary>Returns configuration errors. An empty list means the mapping is valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (StatusMap.Count == 0)
        {
            errors.Add($"{Id}: statusMap is empty.");
        }

        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (jiraStatus, adoState) in StatusMap)
        {
            if (reverse.TryGetValue(adoState, out var other))
            {
                errors.Add(
                    $"{Id}: Jira statuses '{other}' and '{jiraStatus}' both map to ADO state '{adoState}'. " +
                    "The mapping must be one-to-one.");
            }
            else
            {
                reverse[adoState] = jiraStatus;
            }
        }

        return errors;
    }

    /// <summary>Translates a Jira status name into the ADO state name, or null if unmapped.</summary>
    public string? ToAdoState(string jiraStatus) =>
        StatusMap.TryGetValue(jiraStatus, out var adoState) ? adoState : null;

    /// <summary>Translates an ADO state name into the Jira status name, or null if unmapped.</summary>
    public string? ToJiraStatus(string adoState)
    {
        foreach (var (jiraStatus, mappedState) in StatusMap)
        {
            if (string.Equals(mappedState, adoState, StringComparison.OrdinalIgnoreCase))
            {
                return jiraStatus;
            }
        }

        return null;
    }
}
