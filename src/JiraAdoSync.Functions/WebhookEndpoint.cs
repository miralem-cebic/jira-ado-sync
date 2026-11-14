using System.Diagnostics.CodeAnalysis;
using System.Net;
using JiraAdoSync.Azure;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace JiraAdoSync.Functions;

/// <summary>The part the two webhook endpoints share: read the request, let the receiver decide, answer with its status (ADR 0007).</summary>
internal static class WebhookEndpoint
{
    /// <summary>Receives one delivery and returns the response of ADR 0007.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure of the receiver must become a 503 (ADR 0007). The vendor retries it. An unhandled exception would become a 500, which ADO treats as enduring.")]
    public static async Task<HttpResponseData> ReceiveAsync(
        HttpRequestData request,
        WebhookReceiver receiver,
        ILogger logger,
        Side source,
        CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(request.Body, cancellationToken).ConfigureAwait(false);
        var signature = FirstHeader(request, "X-Hub-Signature");
        var authorization = FirstHeader(request, "Authorization");

        ReceiveResult result;
        try
        {
            result = await receiver.ReceiveAsync(new WebhookRequest(source, body, signature, authorization), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SyncLog.DeliveryReceiveFailed(logger, source, ex);
            result = ReceiveResult.Unavailable;
        }

        SyncLog.DeliveryAnswered(logger, source, result);

        var response = request.CreateResponse((HttpStatusCode)result.ToHttpStatusCode());
        foreach (var header in ResponseHeaders.Security)
        {
            response.Headers.Add(header.Key, header.Value);
        }

        return response;
    }

    private static async Task<byte[]> ReadBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await body.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static string? FirstHeader(HttpRequestData request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
}
