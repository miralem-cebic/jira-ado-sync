using System.Text;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Ado;
using JiraAdoSync.Core.Config;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Adapters;

/// <summary>
/// Writes to Azure DevOps through the Work Items REST API 7.1. It reads the revision first and sends it in the JSON Patch
/// <c>test</c> operation, so a write that raced another change fails. The host supplies the <see cref="HttpClient"/> with
/// the organization as base address and the identity credentials. Failed writes throw, so the queue retries.
/// </summary>
public sealed class AdoClient : IAdoClient
{
    private const string ApiVersion = "7.1";

    private readonly HttpClient _http;

    /// <param name="http">The configured client for the Azure DevOps organization.</param>
    public AdoClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc />
    public async Task ApplyAsync(string workItemId, CanonicalItem item, ProjectMapping mapping, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workItemId);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(mapping);

        if (item.Title is null && item.Description is null && item.JiraStatus is null)
        {
            return;
        }

        var path = $"{Uri.EscapeDataString(mapping.AdoProject)}/_apis/wit/workitems/{Uri.EscapeDataString(workItemId)}"
            + $"?api-version={ApiVersion}";

        var revision = await ReadRevisionAsync(path, cancellationToken).ConfigureAwait(false);
        var patch = AdoPatchBuilder.Build(revision, item, mapping);

        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<int> ReadRevisionAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(json)?["rev"]?.GetValue<int>()
            ?? throw new InvalidOperationException("The work item response contains no revision.");
    }
}
