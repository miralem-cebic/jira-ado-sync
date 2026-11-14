namespace JiraAdoSync.Azure;

/// <summary>The limits of the webhook endpoints (ADR 0009).</summary>
public static class WebhookLimits
{
    /// <summary>
    /// The largest request body the endpoints read. Azure DevOps documents a 2 MB payload limit for service hooks. A larger
    /// body is answered with 413 before it is read to the end, and before it is authenticated or stored.
    /// </summary>
    public const int MaxBodyBytes = 2 * 1024 * 1024;
}
