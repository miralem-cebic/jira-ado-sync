using System.Security.Cryptography;
using System.Text;

namespace JiraAdoSync.Core.Webhooks;

/// <summary>
/// Authenticates webhook deliveries (ADR 0006). Pure functions over the raw body and the header values, with no I/O.
/// Headers come from the internet, so a malformed one is rejected and never throws. An empty secret is a configuration
/// error and throws.
/// </summary>
public static class WebhookAuthenticator
{
    private const string JiraPrefix = "sha256=";
    private const string BasicScheme = "Basic ";
    private const int Sha256Bytes = 32;
    private const int Sha256HexChars = Sha256Bytes * 2;

    /// <summary>
    /// Checks a Jira delivery: <c>X-Hub-Signature</c> must be <c>sha256=</c> followed by the hex HMAC-SHA256 of the raw body.
    /// The comparison runs in constant time.
    /// </summary>
    /// <param name="rawBody">The body exactly as received. A re-serialised body does not match.</param>
    /// <param name="signatureHeader">The value of <c>X-Hub-Signature</c>, or null when the header is missing.</param>
    /// <param name="secret">The secret of the Jira webhook.</param>
    /// <exception cref="ArgumentException"><paramref name="secret"/> is null or empty.</exception>
    public static bool IsValidJiraSignature(ReadOnlySpan<byte> rawBody, string? signatureHeader, string secret)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        if (signatureHeader is null || !signatureHeader.StartsWith(JiraPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var hex = signatureHeader.AsSpan(JiraPrefix.Length);
        if (hex.Length != Sha256HexChars)
        {
            return false;
        }

        Span<byte> provided = stackalloc byte[Sha256Bytes];
        if (!TryDecodeHex(hex, provided))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[Sha256Bytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), rawBody, expected);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    /// <summary>
    /// Checks an ADO service hook request: the Authorization header must carry basic credentials that match the configured
    /// user name and password. Both comparisons run in constant time, and both always run.
    /// </summary>
    /// <param name="authorizationHeader">The Authorization header value, or null when it is missing.</param>
    /// <param name="expectedUserName">The user name configured in the service hook.</param>
    /// <param name="expectedPassword">The password configured in the service hook.</param>
    /// <exception cref="ArgumentException">The expected user name or password is null or empty.</exception>
    public static bool IsValidBasicAuthorization(string? authorizationHeader, string expectedUserName, string expectedPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(expectedUserName);
        ArgumentException.ThrowIfNullOrEmpty(expectedPassword);

        if (authorizationHeader is null
            || !authorizationHeader.StartsWith(BasicScheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var encoded = authorizationHeader.AsSpan(BasicScheme.Length);
        var decoded = new byte[encoded.Length];
        if (!Convert.TryFromBase64Chars(encoded, decoded, out var written))
        {
            return false;
        }

        var colon = decoded.AsSpan(0, written).IndexOf((byte)':');
        if (colon < 0)
        {
            return false;
        }

        var user = decoded.AsSpan(0, colon);
        var password = decoded.AsSpan(colon + 1, written - colon - 1);

        var userMatches = CryptographicOperations.FixedTimeEquals(user, Encoding.UTF8.GetBytes(expectedUserName));
        var passwordMatches = CryptographicOperations.FixedTimeEquals(password, Encoding.UTF8.GetBytes(expectedPassword));
        return userMatches & passwordMatches;
    }

    /// <summary>Decodes exactly <c>destination.Length</c> bytes of hex. Returns false for any other input, and never throws.</summary>
    private static bool TryDecodeHex(ReadOnlySpan<char> hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2)
        {
            return false;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            var high = HexValue(hex[2 * i]);
            var low = HexValue(hex[(2 * i) + 1]);
            if (high < 0 || low < 0)
            {
                return false;
            }

            destination[i] = (byte)((high << 4) | low);
        }

        return true;
    }

    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
