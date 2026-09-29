using System;
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
    /// The numbers were tuned on a real medium widget (about 42 Hebrew characters per line, 13 lines of 14px);
    /// small and large are scaled from that. Adjust them here if a size clips or leaves space.
    /// </summary>
    internal static class FeedLayout
    {
        private readonly record struct Layout(int MaxItems, int Budget, int CharsPerLine, int FirstMaxLines, int OtherMaxLines);

        private static Layout For(WidgetSize size) => size switch
        {
            WidgetSize.Small => new Layout(MaxItems: 1, Budget: 6, CharsPerLine: 20, FirstMaxLines: 5, OtherMaxLines: 5),
            WidgetSize.Medium => new Layout(MaxItems: 4, Budget: 13, CharsPerLine: 42, FirstMaxLines: 5, OtherMaxLines: 4),
            _ => new Layout(MaxItems: 10, Budget: 26, CharsPerLine: 42, FirstMaxLines: 5, OtherMaxLines: 4),
        };

        private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        // The board draws the widget name and icon itself, so the card only shows a notice when the data is old.
        private static readonly string Template = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "rtl": true,
              "lang": "he",
              "body": [
                {
                  "type": "Container",
                  "isVisible": "${hasNotice}",
                  "selectAction": { "type": "Action.Execute", "verb": "refresh" },
                  "items": [
                    { "type": "TextBlock", "text": "${notice}", "size": "Small", "isSubtle": true, "wrap": true }
                  ]
                },
                {
                  "type": "Container",
                  "selectAction": { "type": "Action.OpenUrl", "url": "__SITE__" },
                  "items": [
                    {
                      "type": "Container",
                      "$data": "${items}",
                      "separator": "${sep}",
                      "spacing": "Medium",
                      "items": [
                        { "type": "TextBlock", "text": "${meta}", "size": "Small", "isSubtle": true, "isVisible": "${hasMeta}" },
                        { "type": "TextBlock", "text": "${line}", "wrap": true, "maxLines": "${maxLines}", "spacing": "None" }
                      ]
                    }
                  ]
                }
              ]
            }
            """.Replace("__SITE__", HagizraApi.SiteUrl);

        public static string TemplateFor(WidgetSize size) => Template;

        public static string BuildData(NewsSnapshot snapshot, WidgetSize size)
        {
            var layout = For(size);
            string notice = BuildNotice(snapshot);

            using var buffer = new MemoryStream(2048);
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteString("notice", notice);
                w.WriteBoolean("hasNotice", notice.Length > 0);
                w.WriteStartArray("items");

                int count = 0, used = 0;
                foreach (var item in snapshot.Items)
                {
                    string line = BuildLine(item);
                    int cap = count == 0 ? layout.FirstMaxLines : layout.OtherMaxLines;
                    // Every flash also costs its time line.
                    int remaining = layout.Budget - used - 1;
                    int visibleLength = line.Length - (item.Title.Length > 0 ? 4 : 0);
                    int lines = Math.Clamp((visibleLength + layout.CharsPerLine - 1) / layout.CharsPerLine, 1, cap);

                    if (lines > remaining)
                    {
                        // A partly fitting flash is worth showing as a two-line teaser; a single stray line is not.
                        if (remaining < 1 || (count > 0 && remaining < 2))
                            break;
                        lines = remaining;
                    }

                    WriteItem(w, BuildMeta(item), line, Math.Min(cap, remaining), separator: count > 0);
                    used += lines + 1;
                    if (++count >= layout.MaxItems || used >= layout.Budget)
                        break;
                }

                if (count == 0)
                {
                    string message = snapshot.Items.Count == 0 && snapshot.IsStale
                        ? "לא ניתן לטעון חדשות כרגע. ננסה שוב בקרוב."
                        : "אין חדשות להצגה.";
                    WriteItem(w, "", message, layout.FirstMaxLines, separator: false);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        private static void WriteItem(Utf8JsonWriter w, string meta, string line, int maxLines, bool separator)
        {
            w.WriteStartObject();
            w.WriteString("meta", meta);
            w.WriteBoolean("hasMeta", meta.Length > 0);
            w.WriteString("line", line);
            w.WriteNumber("maxLines", maxLines);
            w.WriteBoolean("sep", separator);
            w.WriteEndObject();
        }

        private static string BuildNotice(NewsSnapshot snapshot)
        {
            if (!snapshot.IsStale)
                return "";
            return snapshot.FetchedAt == default
                ? "אין חיבור · לחיצה לרענון"
                : $"לא מעודכן (מ-{FormatTime(snapshot.FetchedAt)}) · לחיצה לרענון";
        }

        private static string BuildMeta(NewsItem item) =>
            item.IsBreaking ? FormatTime(item.Time) + " · 🔴 מבזק חם" : FormatTime(item.Time);

        /// <summary>Markdown for the card: the title is bold, everything else is plain text (the normalizer removed markup characters).</summary>
        internal static string BuildLine(NewsItem item)
        {
            var sb = new StringBuilder();
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
