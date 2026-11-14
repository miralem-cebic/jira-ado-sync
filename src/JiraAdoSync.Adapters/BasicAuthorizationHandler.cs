using System.Net.Http.Headers;
using System.Text;

namespace JiraAdoSync.Adapters;

/// <summary>
/// Sends <c>Authorization: Basic</c> on every request. Jira uses it with the account e-mail and the API token. An Azure
/// DevOps personal access token is sent with an empty user name. The secret never reaches the configuration.
/// </summary>
public sealed class BasicAuthorizationHandler : DelegatingHandler
{
    private readonly AuthenticationHeaderValue _header;

    /// <param name="userName">The user name, or an empty string for a personal access token.</param>
    /// <param name="secret">The API token or the personal access token.</param>
    public BasicAuthorizationHandler(string userName, string secret)
    {
        ArgumentNullException.ThrowIfNull(userName);
        ArgumentException.ThrowIfNullOrEmpty(secret);

        _header = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ":" + secret)));
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Authorization = _header;
        return base.SendAsync(request, cancellationToken);
    }
}
