namespace JiraAdoSync.Core.Config;

/// <summary>Which fields take part in the sync. Disabled fields are ignored in both directions.</summary>
/// <param name="Title">Sync the title (Jira summary, ADO System.Title).</param>
/// <param name="Description">Sync the description as plain text.</param>
/// <param name="Status">Sync the status through the status map.</param>
/// <param name="Assignee">Sync the assignee. Not implemented yet; it needs a user mapping.</param>
public sealed record FeatureFlags(
    bool Title = true,
    bool Description = true,
    bool Status = true,
    bool Assignee = true);
