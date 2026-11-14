using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Ports;

/// <summary>
/// Writes to Azure DevOps. The host implements it with the Work Items REST API; <see cref="Ado.AdoPatchBuilder"/>
/// builds the body.
/// </summary>
public interface IAdoClient
{
    /// <summary>
    /// Writes the non-null fields of <paramref name="item"/> to the work item and translates the status through
    /// <paramref name="mapping"/>. The implementation reads the revision first, so that the write fails when the item
    /// changed in between. Throws when the write fails, so that the caller can retry.
    /// </summary>
    /// <param name="workItemId">Azure DevOps work item id.</param>
    /// <param name="item">The field values to write. Null fields are left unchanged.</param>
    /// <param name="mapping">The mapping that translates the Jira status into the ADO state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    Task ApplyAsync(string workItemId, CanonicalItem item, ProjectMapping mapping, CancellationToken cancellationToken);
}
