namespace JiraAdoSync.Core.Model;

/// <summary>The two systems that are kept in sync.</summary>
public enum Side
{
    /// <summary>Jira Cloud.</summary>
    Jira,

    /// <summary>Azure DevOps.</summary>
    Ado,
}
