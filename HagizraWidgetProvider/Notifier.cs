using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using HagizraWidget.Core;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HagizraWidgetProvider
{
    /// <summary>Optional background poller. Mostly asleep on a wait handle; exits as soon as notifications are switched off.</summary>
    internal static class Notifier
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
        private const int DigestThreshold = 2;

        public static int Run()
        {
            using var mutex = new Mutex(true, @"Local\HagizraWidget.Notifier", out bool owned);
            if (!owned)
                return 0;

            using var wake = new EventWaitHandle(false, EventResetMode.AutoReset, NotificationSwitch.ChangedEventName);
            try
            {
                while (true)
                {
                    if (!SettingsStore.Load().Notifications)
                    {
                        NotifyState.Reset();
                        return 0;
                    }

                    try
                    {
                        Poll();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Notifier", ex);
                    }

                    wake.WaitOne(PollInterval);
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private static void Poll()
        {
            var snapshot = NewsService.GetAsync().GetAwaiter().GetResult();
            if (snapshot.Items.Count == 0)
                return;

            long newest = snapshot.Items.Max(i => i.Id);
            long last = NotifyState.LastId;
            if (last == 0)
            {
                // First run after enabling: remember where we are instead of announcing old flashes.
                NotifyState.Save(newest);
                return;
            }

            var fresh = snapshot.Items
                .Where(i => i.Id > last && !i.IsSilent)
                .OrderBy(i => i.Id)
                .ToList();
            if (newest > last)
                NotifyState.Save(newest);
            if (fresh.Count > 0)
                Show(fresh);
        }

        private static void Show(IReadOnlyList<NewsItem> fresh)
        {
            string headline, line1 = "", line2 = "";
            if (fresh.Count < DigestThreshold)
            {
                var item = fresh[0];
                headline = item.Title.Length > 0 ? item.Title : "הגזרה";
                line1 = item.Body.Length > 0 ? item.Body : FeedLayout.MediaLabel(item.Media);
            }
            else
            {
                // A burst (or a night's worth after wake-up) becomes one toast instead of flooding the action center.
                headline = $"{fresh.Count} מבזקים חדשים בהגזרה";
                var latest = fresh.OrderByDescending(i => i.Id).Take(2).Select(Summary).ToList();
                line1 = latest[0];
                line2 = latest.Count > 1 ? latest[1] : "";
            }

            string xml =
                $"<toast launch=\"{HagizraApi.SiteUrl}\" activationType=\"protocol\"><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(headline)}</text>" +
                $"<text hint-maxLines=\"3\">{SecurityElement.Escape(line1)}</text>" +
                (line2.Length > 0 ? $"<text hint-maxLines=\"2\">{SecurityElement.Escape(line2)}</text>" : "") +
                "</binding></visual></toast>";

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var toast = new ToastNotification(doc)
            {
                Tag = "n" + fresh[^1].Id,
                Group = "hagizra",
                ExpirationTime = DateTimeOffset.Now.AddHours(12),
            };
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        }

        private static string Summary(NewsItem item) =>
            item.Title.Length > 0 ? item.Title : item.Body.Length > 0 ? item.Body : FeedLayout.MediaLabel(item.Media);
    }
}
