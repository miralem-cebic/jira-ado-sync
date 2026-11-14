using System.Net;
using Azure.Core;

namespace JiraAdoSync.Azure.Tests;

public class BearerAuthorizationHandlerTests
{
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    [Fact]
    public async Task SendsTheTokenOfTheCredentialForTheAzureDevOpsScope()
    {
        var credential = new FixedTokenCredential("token-value");
        var inner = new CapturingHandler();
        var handler = new BearerAuthorizationHandler(credential, AzureDevOpsScope) { InnerHandler = inner };
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://dev.example.test/") };

        using var response = await client.GetAsync(new Uri("Project/_apis/wit/workitems/1", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer token-value", inner.Authorization);
        Assert.Equal([AzureDevOpsScope], Assert.Single(credential.ScopesRequested));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class FixedTokenCredential(string token) : TokenCredential
    {
        public List<string[]> ScopesRequested { get; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            Issue(requestContext);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Issue(requestContext));

        private AccessToken Issue(TokenRequestContext requestContext)
        {
            ScopesRequested.Add(requestContext.Scopes);
            return new AccessToken(token, new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }
    }
}
