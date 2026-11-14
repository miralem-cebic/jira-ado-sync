using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Ports;

/// <summary>
/// Writes to Jira Cloud. The host implements it with the REST API v3; <see cref="Jira.JiraRequests"/> builds the bodies.
/// </summary>
public interface IJiraClient
{
    /// <summary>
    /// Writes the non-null fields of <paramref name="item"/> to the issue. A status change is made through a transition.
    /// Throws when the write fails, so that the caller can retry.
    /// </summary>
    /// <param name="issueKey">Jira issue key, e.g. <c>PROJ-123</c>.</param>
    /// <param name="item">The field values to write. Null fields are left unchanged.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task ApplyAsync(string issueKey, CanonicalItem item, CancellationToken cancellationToken);
}
