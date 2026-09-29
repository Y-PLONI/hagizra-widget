using System.Net;
using System.Text.RegularExpressions;

namespace HagizraWidget.Core;

/// <summary>Turns the raw markdown/HTML mix of a Hagizra message into plain title, body and media flags.</summary>
public static partial class NewsNormalizer
{
    [GeneratedRegex(@"\[(image|video|audio)-embedded#\]\([^)]*\)", RegexOptions.IgnoreCase)]
    private static partial Regex EmbeddedRegex();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex MarkdownImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]{0,200}>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"^[\s\u200e\u200f]*\*{2,}(?<t>[^*]+?)\*{2,}")]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpaceRegex();

    private static readonly char[] Edge =
    {
        ' ', '\u200e', '\u200f', '\u202a', '\u202b', '\u202c', '\u202d', '\u202e', '\u2066', '\u2067', '\u2068', '\u2069',
    };

    public static (string Title, string Body, MediaKind Media) Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return ("", "", MediaKind.None);

        var media = MediaKind.None;
        string text = EmbeddedRegex().Replace(raw, m =>
        {
            media |= m.Groups[1].Value.ToLowerInvariant() switch
            {
                "video" => MediaKind.Video,
                "audio" => MediaKind.Audio,
                _ => MediaKind.Image,
            };
            return " ";
        });

        text = MarkdownImageRegex().Replace(text, _ =>
        {
            media |= MediaKind.Image;
            return " ";
        });
        text = LinkRegex().Replace(text, "$1");
        text = WebUtility.HtmlDecode(TagRegex().Replace(text, ""));

        string title = "";
        var match = TitleRegex().Match(text);
        if (match.Success)
        {
            title = Clean(match.Groups["t"].Value.TrimEnd('|'));
            text = text[match.Length..];
        }

        return (title, Clean(text), media);
    }

    private static string Clean(string s)
    {
        s = s.Replace("*", "").Replace('_', ' ');
        return SpaceRegex().Replace(s, " ").Trim(Edge);
    }
}
