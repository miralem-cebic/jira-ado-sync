using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>The Jira webhook endpoint. Authentication is the HMAC of the body, so the Function level is anonymous.</summary>
public sealed class JiraWebhookFunction(WebhookReceiver receiver, ILogger<JiraWebhookFunction> logger)
{
    /// <summary>Receives a Jira delivery.</summary>
    [Function("JiraWebhook")]
    public Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/jira")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WebhookEndpoint.ReceiveAsync(request, receiver, logger, Side.Jira, cancellationToken);
    }
}
