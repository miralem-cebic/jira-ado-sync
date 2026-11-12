using System.Net;
using System.Text.RegularExpressions;

namespace JiraAdoSync.Core.Ado;

/// <summary>
/// Converts between Azure DevOps HTML fields (e.g. System.Description) and plain text.
/// Both directions are chosen so that a value written by us reads back as the same plain text.
/// </summary>
public static class AdoHtml
{
    private static readonly Regex LineBreak = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlockEnd = new(@"</(p|div|li|h[1-6])\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnyTag = new("<[^>]+>", RegexOptions.Compiled);

    /// <summary>Converts an HTML field value to plain text: line breaks and block ends become newlines, tags are dropped.</summary>
    /// <param name="html">The HTML value, or null.</param>
    /// <returns>The plain text, or null if <paramref name="html"/> is null.</returns>
    public static string? ToPlainText(string? html)
    {
        if (html is null)
        {
            return null;
        }

        var text = LineBreak.Replace(html, "\n");
        text = BlockEnd.Replace(text, "\n");
        text = AnyTag.Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace('\u00A0', ' ').Replace("\r\n", "\n", StringComparison.Ordinal);

        return text.TrimEnd('\n');
    }

    /// <summary>Converts plain text to an HTML field value: characters are encoded, newlines become <c>&lt;br&gt;</c>.</summary>
    /// <param name="text">The plain text, or null.</param>
    /// <returns>The HTML value, or null if <paramref name="text"/> is null.</returns>
    public static string? FromPlainText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return string.Join("<br>", lines.Select(line => WebUtility.HtmlEncode(line)));
    }
}
