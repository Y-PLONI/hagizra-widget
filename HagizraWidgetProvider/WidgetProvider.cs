using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HagizraWidget.Core;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace HagizraWidgetProvider
{
    internal class WidgetProvider : IWidgetProvider, IWidgetProvider2
    {
        public const string WidgetProviderClassId = "5C0D8E3A-7B21-4F6A-9E5D-2A8B1C4D6F90";

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

        // Accessed from concurrent COM threads.
        private static readonly ConcurrentDictionary<string, WidgetSize> ActiveWidgets = new();

        // Widgets the board currently shows; only these are refreshed on the timer.
        private static readonly ConcurrentDictionary<string, byte> Visible = new();

        // Widgets currently showing the settings form, with the validation error to show (or "").
        private static readonly ConcurrentDictionary<string, string> Customizing = new();

        // Last card sent per widget, so the timer skips updates that would change nothing.
        private static readonly ConcurrentDictionary<string, string> LastSent = new();

        private static readonly object TimerGate = new();
        private static Timer? _timer;

        /// <summary>Picks up widgets that already exist when Windows relaunches the provider (runs on first activation).</summary>
        static WidgetProvider()
        {
            try
            {
                var infos = WidgetManager.GetDefault()?.GetWidgetInfos();
                if (infos == null)
                    return;
                foreach (var info in infos)
                    ActiveWidgets[info.WidgetContext.Id] = info.WidgetContext.Size;
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
            }
        }

        public void CreateWidget(WidgetContext widgetContext) => Show(widgetContext);

        public void Activate(WidgetContext widgetContext) => Show(widgetContext);

        private static void Show(WidgetContext context)
        {
            ActiveWidgets[context.Id] = context.Size;
            Visible[context.Id] = 0;
            StartTimer();
            _ = UpdateWidgetAsync(context.Id, context.Size, force: false, fromTimer: false);
        }

        public void Deactivate(string widgetId)
        {
            Visible.TryRemove(widgetId, out _);
            StopTimerIfIdle();
        }

        public void DeleteWidget(string widgetId, string customState)
        {
            ActiveWidgets.TryRemove(widgetId, out _);
            Visible.TryRemove(widgetId, out _);
            Customizing.TryRemove(widgetId, out _);
            LastSent.TryRemove(widgetId, out _);
            StopTimerIfIdle();
            if (ActiveWidgets.IsEmpty)
                Program.ExitEvent.Set();
        }

        public void OnWidgetContextChanged(WidgetContextChangedArgs args)
        {
            ActiveWidgets[args.WidgetContext.Id] = args.WidgetContext.Size;
            _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.Size, force: false, fromTimer: false);
        }

        public void OnActionInvoked(WidgetActionInvokedArgs args)
        {
            string id = args.WidgetContext.Id;
            var size = args.WidgetContext.Size;

            switch (args.Verb)
            {
                case "refresh":
                    _ = UpdateWidgetAsync(id, size, force: true, fromTimer: false);
                    break;

                case "save":
                    _ = SaveAsync(id, size, args.Data);
                    break;

                case "cancel":
                    Customizing.TryRemove(id, out _);
                    _ = UpdateWidgetAsync(id, size, force: false, fromTimer: false);
                    break;
            }
        }

        /// <summary>The board's own "Customize widget" menu item.</summary>
        public void OnCustomizationRequested(WidgetCustomizationRequestedArgs args)
        {
            Customizing[args.WidgetContext.Id] = "";
            _ = UpdateWidgetAsync(args.WidgetContext.Id, args.WidgetContext.Size, force: false, fromTimer: false);
        }

        private static async Task SaveAsync(string id, WidgetSize size, string data)
        {
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrEmpty(data) ? "{}" : data);
                var previous = SettingsStore.Load();
                var settings = new WidgetSettings
                {
                    Notifications = SettingsForm.ReadBool(doc.RootElement, "notifications", previous.Notifications),
                };

                // The notifier reads the saved settings, so they must be on disk before it starts.
                SettingsStore.Save(settings);
                if (settings.Notifications != previous.Notifications)
                {
                    string error = await NotificationSwitch.ApplyAsync(settings.Notifications).ConfigureAwait(false);
                    if (error.Length > 0)
                    {
                        settings.Notifications = previous.Notifications;
                        SettingsStore.Save(settings);
                        Customizing[id] = error;
                        await UpdateWidgetAsync(id, size, force: false, fromTimer: false).ConfigureAwait(false);
                        return;
                    }
                }

                Customizing.TryRemove(id, out _);
                // Settings are shared, so every widget instance re-renders.
                foreach (var widget in ActiveWidgets)
                    _ = UpdateWidgetAsync(widget.Key, widget.Value, force: false, fromTimer: false);
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
                Customizing[id] = "שמירת ההגדרות נכשלה";
                await UpdateWidgetAsync(id, size, force: false, fromTimer: false).ConfigureAwait(false);
            }
        }

        private static void StartTimer()
        {
            lock (TimerGate)
                _timer ??= new Timer(_ => RefreshVisible(), null, RefreshInterval, RefreshInterval);
        }

        private static void StopTimerIfIdle()
        {
            lock (TimerGate)
            {
                if (!Visible.IsEmpty)
                    return;
                _timer?.Dispose();
                _timer = null;
            }
        }

        private static void RefreshVisible()
        {
            foreach (var id in Visible.Keys)
            {
                if (ActiveWidgets.TryGetValue(id, out var size))
                    _ = UpdateWidgetAsync(id, size, force: false, fromTimer: true);
            }
        }

        private static async Task UpdateWidgetAsync(string widgetId, WidgetSize size, bool force, bool fromTimer)
        {
            try
            {
                var settings = SettingsStore.Load();

                if (Customizing.TryGetValue(widgetId, out string? error))
                {
                    if (fromTimer)
                        return;

                    LastSent.TryRemove(widgetId, out _);
                    WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(widgetId)
                    {
                        Template = SettingsForm.Build(settings, error, compact: size == WidgetSize.Small),
                        Data = "{}",
                        CustomState = ""
                    });
                    return;
                }

                // All widgets share one cached fetch, so a board open or resize usually costs no network.
                var snapshot = await NewsService.GetAsync(force).ConfigureAwait(false);
                string data = FeedLayout.BuildData(snapshot, size);

                string key = size + "|" + data;
                if (fromTimer && LastSent.TryGetValue(widgetId, out string? last) && last == key)
                    return;
                LastSent[widgetId] = key;

                WidgetManager.GetDefault().UpdateWidget(new WidgetUpdateRequestOptions(widgetId)
                {
                    Template = FeedLayout.TemplateFor(size),
                    Data = data,
                    CustomState = ""
                });
            }
            catch (Exception ex)
            {
                Log.Error("WidgetProvider", ex);
            }
        }
    }
}
