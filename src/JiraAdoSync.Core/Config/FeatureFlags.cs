namespace JiraAdoSync.Core.Config;

/// <summary>Which fields take part in the sync. Disabled fields are ignored in both directions.</summary>
public sealed record FeatureFlags(
    bool Title = true,
    bool Description = true,
    bool Status = true,
    bool Assignee = true);
