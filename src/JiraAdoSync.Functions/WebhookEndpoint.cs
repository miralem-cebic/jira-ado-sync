using System.Net;
using JiraAdoSync.Azure;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>
/// The part the two webhook endpoints share. The decisions are in <c>WebhookDelivery</c>. This class only reads the headers,
/// logs the decision and writes the response, with the security headers (ADR 0007, ADR 0009).
/// </summary>
internal static class WebhookEndpoint
{
    /// <summary>Receives one delivery and returns the response.</summary>
    public static async Task<HttpResponseData> ReceiveAsync(
        HttpRequestData request,
        WebhookReceiver receiver,
        ILogger logger,
        Side source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(receiver);
        ArgumentNullException.ThrowIfNull(logger);

        var answer = await WebhookDelivery.HandleAsync(
            request.Body,
            WebhookDelivery.ParseContentLength(FirstHeader(request, "Content-Length")),
            source,
            FirstHeader(request, "X-Hub-Signature"),
            FirstHeader(request, "Authorization"),
            receiver,
            cancellationToken).ConfigureAwait(false);

        Log(logger, source, answer);

        var response = request.CreateResponse((HttpStatusCode)answer.StatusCode);
        foreach (var header in ResponseHeaders.Security)
        {
            response.Headers.Add(header.Key, header.Value);
        }

        return response;
    }

    private static void Log(ILogger logger, Side source, WebhookAnswer answer)
    {
        if (answer.Failed)
        {
            SyncLog.DeliveryReceiveFailed(logger, source, answer.Error);
        }
        else if (answer.TooLarge)
        {
            SyncLog.DeliveryTooLarge(logger, source, WebhookLimits.MaxBodyBytes);
        }
        else
        {
            SyncLog.DeliveryAnswered(logger, source, answer.Result!.Value);
        }
    }

    private static string? FirstHeader(HttpRequestData request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
