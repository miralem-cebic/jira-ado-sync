using System.Net.Http.Headers;
using Azure.Core;

namespace JiraAdoSync.Azure;

/// <summary>
/// Sends a bearer token from an Azure credential on every request. Azure DevOps uses it with the managed identity of the
/// Function. The token is requested per attempt, so a renewed token is picked up without a restart.
/// </summary>
public sealed class BearerAuthorizationHandler : DelegatingHandler
{
    private readonly TokenCredential _credential;
    private readonly string[] _scopes;

    /// <param name="credential">The credential that issues the token, for example a managed identity.</param>
    /// <param name="scope">The scope of the token, for example the Azure DevOps resource with <c>/.default</c>.</param>
    public BearerAuthorizationHandler(TokenCredential credential, string scope)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        ArgumentException.ThrowIfNullOrEmpty(scope);
        _scopes = [scope];
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var token = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
