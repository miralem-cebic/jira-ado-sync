using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>The Azure DevOps service hook endpoint. Authentication is basic, checked by the receiver.</summary>
public sealed class AdoWebhookFunction(WebhookReceiver receiver, ILogger<AdoWebhookFunction> logger)
{
    /// <summary>Receives an Azure DevOps service hook delivery.</summary>
    [Function("AdoWebhook")]
    public Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/ado")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return WebhookEndpoint.ReceiveAsync(request, receiver, logger, Side.Ado, cancellationToken);
    }
}
