using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HagizraWidget.Core;
using Microsoft.Windows.Widgets;

namespace HagizraWidgetProvider
{
    /// <summary>
    /// Widgets cannot scroll, so each size gets a fixed line budget and the newest flashes are packed into it.
    /// The numbers are estimates of what fits at 14px; adjust them here if a size clips or leaves space.
    /// </summary>
    internal static class FeedLayout
    {
        private readonly record struct Layout(bool Header, int MaxItems, int Budget, int CharsPerLine, int FirstMaxLines, int OtherMaxLines);

        private static Layout For(WidgetSize size) => size switch
        {
            WidgetSize.Small => new Layout(Header: false, MaxItems: 1, Budget: 5, CharsPerLine: 17, FirstMaxLines: 5, OtherMaxLines: 5),
            WidgetSize.Medium => new Layout(Header: false, MaxItems: 3, Budget: 6, CharsPerLine: 36, FirstMaxLines: 3, OtherMaxLines: 2),
            _ => new Layout(Header: true, MaxItems: 8, Budget: 12, CharsPerLine: 36, FirstMaxLines: 4, OtherMaxLines: 3),
        };

        private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        private static readonly string TemplateWithHeader = BuildTemplate(header: true);
        private static readonly string TemplatePlain = BuildTemplate(header: false);

        public static string TemplateFor(WidgetSize size) => For(size).Header ? TemplateWithHeader : TemplatePlain;

        private static string BuildTemplate(bool header)
        {
            const string headerBlock = """
                {
                  "type": "Container",
                  "selectAction": { "type": "Action.Execute", "verb": "refresh" },
                  "items": [
                    { "type": "TextBlock", "text": "${header}", "size": "Small", "isSubtle": true, "weight": "Bolder" }
                  ]
                },
                """;

            const string card = """
                {
                  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "rtl": true,
                  "lang": "he",
                  "body": [
                    __HEADER__
                    {
                      "type": "Container",
                      "selectAction": { "type": "Action.OpenUrl", "url": "__SITE__" },
                      "items": [
                        {
                          "type": "TextBlock",
                          "$data": "${items}",
                          "text": "${line}",
                          "wrap": true,
                          "maxLines": "${maxLines}",
                          "separator": "${sep}",
                          "spacing": "Small"
                        }
                      ]
                    }
                  ]
                }
                """;

            return card.Replace("__HEADER__", header ? headerBlock : "").Replace("__SITE__", HagizraApi.SiteUrl);
        }

        public static string BuildData(NewsSnapshot snapshot, WidgetSize size)
        {
            var layout = For(size);
            using var buffer = new MemoryStream(2048);
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteString("header", BuildHeader(snapshot));
                w.WriteStartArray("items");

                int count = 0, used = 0;
                foreach (var item in snapshot.Items)
                {
                    string line = BuildLine(item);
                    int cap = count == 0 ? layout.FirstMaxLines : layout.OtherMaxLines;
                    int remaining = layout.Budget - used;
                    int visibleLength = line.Length - (item.Title.Length > 0 ? 4 : 0);
                    int need = Math.Clamp((visibleLength + layout.CharsPerLine - 1) / layout.CharsPerLine, 1, cap);

                    if (need > remaining)
                    {
                        // A partly fitting flash is worth showing as a two-line teaser; a single stray line is not.
                        if (count > 0 && remaining < 2)
                            break;
                        need = remaining;
                    }

                    WriteItem(w, line, Math.Min(cap, remaining), separator: count > 0);
                    used += need;
                    if (++count >= layout.MaxItems || used >= layout.Budget)
                        break;
                }

                if (count == 0)
                {
                    string message = snapshot.Items.Count == 0 && snapshot.IsStale
                        ? "לא ניתן לטעון חדשות כרגע. ננסה שוב בקרוב."
                        : "אין חדשות להצגה.";
                    WriteItem(w, message, layout.FirstMaxLines, separator: false);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        private static void WriteItem(Utf8JsonWriter w, string line, int maxLines, bool separator)
        {
            w.WriteStartObject();
            w.WriteString("line", line);
            w.WriteNumber("maxLines", maxLines);
            w.WriteBoolean("sep", separator);
            w.WriteEndObject();
        }

        private static string BuildHeader(NewsSnapshot snapshot)
        {
            if (!snapshot.IsStale)
                return "↻ הגזרה";
            return snapshot.FetchedAt == default
                ? "↻ הגזרה · אין חיבור"
                : $"↻ הגזרה · לא מעודכן (מ-{FormatTime(snapshot.FetchedAt)})";
        }

        /// <summary>Markdown for the card: the title is bold, everything else is plain text (the normalizer removed markup characters).</summary>
        internal static string BuildLine(NewsItem item)
        {
            var sb = new StringBuilder(FormatTime(item.Time)).Append(" · ");
            if (item.IsBreaking)
                sb.Append("🔴 ");
            if (item.Title.Length > 0)
                sb.Append("**").Append(item.Title).Append(" |** ");
            sb.Append(item.Body);

            if (item.Title.Length == 0 && item.Body.Length == 0)
                sb.Append(MediaLabel(item.Media));
            else
                sb.Append(MediaMark(item.Media));
            return sb.ToString().TrimEnd();
        }

        private static string MediaMark(MediaKind media)
        {
            var sb = new StringBuilder();
            if (media.HasFlag(MediaKind.Image)) sb.Append(" 📷");
            if (media.HasFlag(MediaKind.Video)) sb.Append(" 🎬");
            if (media.HasFlag(MediaKind.Audio)) sb.Append(" 🎧");
            return sb.ToString();
        }

        internal static string MediaLabel(MediaKind media) =>
            media.HasFlag(MediaKind.Video) ? "🎬 וידאו" :
            media.HasFlag(MediaKind.Audio) ? "🎧 אודיו" :
            "📷 תמונה";

        internal static string FormatTime(DateTimeOffset time)
        {
            var local = time.ToLocalTime();
            return local.Date == DateTime.Today
                ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
                : local.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
