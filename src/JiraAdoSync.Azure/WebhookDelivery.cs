using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using JiraAdoSync.Core.Delivery;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Azure;

/// <summary>
/// Takes one webhook request from the body to the answer (ADR 0007, ADR 0009). The Functions endpoints call it. It has no
/// Functions types, so the decisions can be tested. The size is checked before the body is read, and the receiver
/// authenticates the request before anything is parsed or stored.
/// </summary>
public static class WebhookDelivery
{
    /// <summary>Parses the Content-Length header. Returns null when it is missing or is not a plain number.</summary>
    public static long? ParseContentLength(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null;

    /// <summary>Handles one request. The result is never an exception: every failure becomes a 503 with the error kept.</summary>
    /// <param name="body">The request body.</param>
    /// <param name="declaredLength">The Content-Length of the request, or null.</param>
    /// <param name="source">The endpoint the request arrived at.</param>
    /// <param name="signatureHeader">The X-Hub-Signature header of a Jira delivery, or null.</param>
    /// <param name="authorizationHeader">The Authorization header of an ADO delivery, or null.</param>
    /// <param name="receiver">The receiver that authenticates and stores the delivery.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure while the body is read or the delivery is received must become a 503 (ADR 0007). The vendor retries it. An unhandled exception would become a 500, which ADO treats as enduring. The error stays in the answer, so that it is logged.")]
    public static async Task<WebhookAnswer> HandleAsync(
        Stream body,
        long? declaredLength,
        Side source,
        string? signatureHeader,
        string? authorizationHeader,
        WebhookReceiver receiver,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(receiver);

        if (declaredLength is > WebhookLimits.MaxBodyBytes)
        {
            return WebhookAnswer.ForTooLarge();
        }

        try
        {
            var bytes = await RequestBodyReader.ReadAsync(body, WebhookLimits.MaxBodyBytes, cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return WebhookAnswer.ForTooLarge();
            }

            var result = await receiver.ReceiveAsync(
                new WebhookRequest(source, bytes, signatureHeader, authorizationHeader),
                cancellationToken).ConfigureAwait(false);

            return WebhookAnswer.ForResult(result);
        }
        catch (Exception error)
        {
            return WebhookAnswer.ForFailure(error);
        }
    }
}
