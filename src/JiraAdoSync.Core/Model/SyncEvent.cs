namespace JiraAdoSync.Core.Model;

/// <summary>
/// A change as received from one side, in that side's own vocabulary.
/// The status is the source-side state name (a Jira status for Jira, an ADO state for ADO).
/// </summary>
public sealed record SyncEvent(
    Side Source,
    bool ChangedByServiceIdentity,
    DateTimeOffset ChangedAt,
    string Title,
    string? Description,
    string? Status,
    string? Assignee);
