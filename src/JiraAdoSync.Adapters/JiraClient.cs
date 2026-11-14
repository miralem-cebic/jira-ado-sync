using System.Text;
using System.Text.Json.Nodes;
using JiraAdoSync.Core.Jira;
using JiraAdoSync.Core.Model;
using JiraAdoSync.Core.Ports;

namespace JiraAdoSync.Adapters;

/// <summary>
/// Writes to Jira Cloud through the REST API v3. The host supplies the <see cref="HttpClient"/> with the site as base
/// address, the service account credentials and the <see cref="RetryingHandler"/>. Failed writes throw, so the queue retries.
/// </summary>
public sealed class JiraClient : IJiraClient
{
    private readonly HttpClient _http;

    /// <param name="http">The configured client for the Jira site.</param>
    public JiraClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    /// <inheritdoc />
    public async Task ApplyAsync(string issueKey, CanonicalItem item, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(issueKey);
        ArgumentNullException.ThrowIfNull(item);

        var issuePath = "rest/api/3/issue/" + Uri.EscapeDataString(issueKey);

        if (item.Title is not null || item.Description is not null)
        {
            await SendAsync(HttpMethod.Put, issuePath, JiraRequests.BuildIssueUpdate(item), cancellationToken)
                .ConfigureAwait(false);
        }

        if (item.JiraStatus is not null)
        {
            await TransitionAsync(issueKey, issuePath, item.JiraStatus, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task TransitionAsync(string issueKey, string issuePath, string targetStatus, CancellationToken cancellationToken)
    {
        var current = await CurrentStatusAsync(issuePath, cancellationToken).ConfigureAwait(false);
        if (string.Equals(current, targetStatus, StringComparison.Ordinal))
        {
            return;
        }

        var transitions = await GetAsync($"{issuePath}/transitions", cancellationToken).ConfigureAwait(false);
        var transitionId = JiraRequests.FindTransitionId(transitions, targetStatus)
            ?? throw new InvalidOperationException(
                $"No Jira transition of {issueKey} leads from '{current}' to '{targetStatus}'. The workflow needs one.");

        await SendAsync(
            HttpMethod.Post,
            $"{issuePath}/transitions",
            JiraRequests.BuildTransition(transitionId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> CurrentStatusAsync(string issuePath, CancellationToken cancellationToken)
    {
        var json = await GetAsync($"{issuePath}?fields=status", cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(json)?["fields"]?["status"]?["name"]?.GetValue<string>();
    }

    private async Task<string> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(HttpMethod method, string path, string body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
