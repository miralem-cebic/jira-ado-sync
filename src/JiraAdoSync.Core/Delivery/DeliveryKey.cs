using System.Security.Cryptography;
using JiraAdoSync.Core.Model;

namespace JiraAdoSync.Core.Delivery;

/// <summary>The name under which a delivery's raw body is stored (ADR 0004).</summary>
public static class DeliveryKey
{
    /// <summary>
    /// Returns <c>jira/</c> or <c>ado/</c> followed by the lower-case hex SHA-256 of the raw body. A redelivery of the same
    /// body therefore gets the same name, and the store keeps one blob.
    /// </summary>
    /// <param name="source">The side that sent the delivery.</param>
    /// <param name="body">The body exactly as received.</param>
    public static string For(Side source, ReadOnlySpan<byte> body)
    {
        var prefix = source switch
        {
            Side.Jira => "jira",
            Side.Ado => "ado",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown side."),
        };

        return prefix + "/" + Convert.ToHexStringLower(SHA256.HashData(body));
    }
}
