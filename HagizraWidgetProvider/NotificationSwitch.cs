using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using HagizraWidget.Core;

namespace HagizraWidgetProvider
{
    /// <summary>Turns the background notifier on or off: a startup task (survives reboots) plus an immediate start/stop.</summary>
    internal static class NotificationSwitch
    {
        public const string TaskId = "HagizraNotifier";
        public const string ChangedEventName = @"Local\HagizraWidget.SettingsChanged";

        /// <summary>Returns an error message for the settings form, or "" on success.</summary>
        public static async Task<string> ApplyAsync(bool enable)
        {
            try
            {
                var task = await StartupTask.GetAsync(TaskId);
                if (enable)
                {
                    var state = task.State;
                    if (state == StartupTaskState.Disabled)
                        state = await task.RequestEnableAsync();
                    if (state != StartupTaskState.Enabled && state != StartupTaskState.EnabledByPolicy)
                        return "ההפעלה עם ווינדוס חסומה. אפשר לאפשר אותה ב: הגדרות ← אפליקציות ← הפעלה.";

                    Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true });
                }
                else
                {
                    task.Disable();
                    NotifyState.Reset();
                }

                using var changed = new EventWaitHandle(false, EventResetMode.AutoReset, ChangedEventName);
                changed.Set();
                return "";
            }
            catch (Exception ex)
            {
                Log.Error("NotificationSwitch", ex);
                return "לא ניתן לעדכן את ההתראות.";
            }
        }
    }
}
