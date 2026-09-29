using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using HagizraWidget.Core;

namespace HagizraWidgetProvider
{
    /// <summary>The widget's "Customize" form. Built directly from the current values, so no data binding is needed.</summary>
    internal static class SettingsForm
    {
        private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <param name="compact">Small size: the widget clips what does not fit, so the buttons come first and the hint is left out.</param>
        public static string Build(WidgetSettings settings, string error, bool compact)
        {
            using var buffer = new MemoryStream(2048);
            using (var w = new Utf8JsonWriter(buffer, WriterOptions))
            {
                w.WriteStartObject();
                w.WriteString("$schema", "http://adaptivecards.io/schemas/adaptive-card.json");
                w.WriteString("type", "AdaptiveCard");
                w.WriteString("version", "1.5");
                w.WriteBoolean("rtl", true);
                w.WriteString("lang", "he");
                w.WriteStartArray("body");

                if (compact)
                {
                    w.WriteStartObject();
                    w.WriteString("type", "ActionSet");
                    WriteFormActions(w);
                    w.WriteEndObject();
                    WriteError(w, error);
                }
                else
                {
                    WriteText(w, "הגדרות", bolder: true);
                }

                WriteToggle(w, "notifications", "התראה על מבזק חדש", settings.Notifications);
                if (!compact)
                    WriteText(w, "ההתראות כבויות כברירת מחדל. כשהן דלוקות, הגזרה נבדקת פעם בדקה ברקע.", subtle: true);

                if (!compact)
                    WriteError(w, error);

                w.WriteEndArray();

                if (!compact)
                    WriteFormActions(w);

                w.WriteEndObject();
            }
            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }

        /// <summary>Toggles arrive as "true"/"false" strings or as booleans depending on the host version.</summary>
        public static bool ReadBool(JsonElement root, string name, bool fallback)
        {
            if (!root.TryGetProperty(name, out var v))
                return fallback;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => string.Equals(v.GetString(), "true", System.StringComparison.OrdinalIgnoreCase),
                _ => fallback,
            };
        }

        private static void WriteToggle(Utf8JsonWriter w, string id, string title, bool value)
        {
            w.WriteStartObject();
            w.WriteString("type", "Input.Toggle");
            w.WriteString("id", id);
            w.WriteString("title", title);
            w.WriteString("value", value ? "true" : "false");
            w.WriteString("valueOn", "true");
            w.WriteString("valueOff", "false");
            w.WriteBoolean("wrap", true);
            w.WriteEndObject();
        }

        private static void WriteFormActions(Utf8JsonWriter w)
        {
            w.WriteStartArray("actions");
            WriteAction(w, "שמור", "save", "auto");
            WriteAction(w, "ביטול", "cancel", "none");
            w.WriteEndArray();
        }

        private static void WriteAction(Utf8JsonWriter w, string title, string verb, string associatedInputs)
        {
            w.WriteStartObject();
            w.WriteString("type", "Action.Execute");
            w.WriteString("title", title);
            w.WriteString("verb", verb);
            w.WriteString("associatedInputs", associatedInputs);
            w.WriteEndObject();
        }

        private static void WriteError(Utf8JsonWriter w, string error)
        {
            if (error.Length == 0)
                return;
            w.WriteStartObject();
            w.WriteString("type", "TextBlock");
            w.WriteString("text", error);
            w.WriteString("color", "Attention");
            w.WriteBoolean("wrap", true);
            w.WriteEndObject();
        }

        private static void WriteText(Utf8JsonWriter w, string text, bool bolder = false, bool subtle = false)
        {
            w.WriteStartObject();
            w.WriteString("type", "TextBlock");
            w.WriteString("text", text);
            w.WriteBoolean("wrap", true);
            if (bolder)
                w.WriteString("weight", "Bolder");
            if (subtle)
            {
                w.WriteBoolean("isSubtle", true);
                w.WriteString("size", "Small");
            }
            w.WriteEndObject();
        }
    }
}
