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

    public static string? ToPlainText(string? html)
    {
        if (html is null)
        {
            return null;
        }

        var text = LineBreak.Replace(html, "\n");
        text = BlockEnd.Replace(text, "\n");
        text = AnyTag.Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text).Replace(' ', ' ').Replace("\r\n", "\n");

        return text.TrimEnd('\n');
    }

    public static string? FromPlainText(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        return string.Join("<br>", lines.Select(line => WebUtility.HtmlEncode(line)));
    }
}
