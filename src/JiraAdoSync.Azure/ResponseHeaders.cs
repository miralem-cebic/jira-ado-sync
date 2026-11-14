namespace JiraAdoSync.Azure;

/// <summary>
/// The baseline headers of every response of the webhook API (roadmap, Phase 1). They match the API rules HOS-009..012
/// of engineering-baseline. The responses carry no body, so each header only limits what a browser may do with one.
/// </summary>
public static class ResponseHeaders
{
    /// <summary>The header name and value pairs that every response gets.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Security { get; } =
    [
        new("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'"),
        new("X-Content-Type-Options", "nosniff"),
        new("Cache-Control", "no-store"),
        new("Strict-Transport-Security", "max-age=31536000"),
    ];
}
