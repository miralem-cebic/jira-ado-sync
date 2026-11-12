using System.Security.Cryptography;
using System.Text;

namespace JiraAdoSync.Core.Model;

/// <summary>
/// Side-neutral representation of the synced fields.
/// Status is always expressed as the Jira status name, so both sides can be compared.
/// A field that is disabled by configuration is null and does not contribute to the hash.
/// </summary>
public sealed record CanonicalItem(
    string? Title,
    string? Description,
    string? JiraStatus,
    string? Assignee)
{
    /// <summary>Stable SHA-256 over the canonical field set. Used for loop prevention.</summary>
    public string Hash()
    {
        var canonical = string.Join(
            '\u001F',
            Title ?? string.Empty,
            Description ?? string.Empty,
            JiraStatus ?? string.Empty,
            Assignee ?? string.Empty);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes);
    }
}
