using System.Text.Json;

namespace HagizraWidget.Core;

/// <summary>
/// Shared news cache: one request feeds every widget instance and the notifier process. The last result
/// is also kept on disk, so the widget can show something immediately and while offline.
/// </summary>
public static class NewsService
{
    private const int Limit = 20;

    // Slightly under the widget's 60 s refresh tick, so every tick really fetches.
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(55);
    private static readonly TimeSpan MinForceInterval = TimeSpan.FromSeconds(5);

    private static readonly object Gate = new();
    private static NewsSnapshot? _snapshot;
    private static DateTime _diskWriteTime;
    private static DateTimeOffset _lastAttempt;
    private static DateTimeOffset _retryAt;
    private static int _failures;
    private static Task<NewsSnapshot>? _inflight;

    public static Task<NewsSnapshot> GetAsync(bool force = false)
    {
        lock (Gate)
        {
            LoadFromDiskIfNewer();
            if (_inflight != null)
                return _inflight;

            var now = DateTimeOffset.UtcNow;
            bool useCached = force
                ? now - _lastAttempt < MinForceInterval
                : (_snapshot != null && now - _snapshot.FetchedAt < Ttl) || now < _retryAt;
            if (useCached)
                return Task.FromResult(Current());

            _lastAttempt = now;
            // Task.Run guarantees the finally block cannot clear _inflight before it is assigned.
            return _inflight = Task.Run(FetchAsync);
        }
    }

    private static NewsSnapshot Current() =>
        _snapshot is null
            ? new NewsSnapshot(Array.Empty<NewsItem>(), default, IsStale: true)
            : _snapshot with { IsStale = _failures > 0 };

    private static async Task<NewsSnapshot> FetchAsync()
    {
        try
        {
            var items = await HagizraApi.FetchAsync(Limit).ConfigureAwait(false);
            var snapshot = new NewsSnapshot(items, DateTimeOffset.UtcNow, IsStale: false);
            lock (Gate)
            {
                _snapshot = snapshot;
                _failures = 0;
                _retryAt = default;
            }
            SaveToDisk(snapshot);
            return snapshot;
        }
        catch (Exception ex)
        {
            Log.Error("NewsService", ex);
            lock (Gate)
            {
                _failures++;
                _retryAt = DateTimeOffset.UtcNow + Backoff(_failures);
                return Current();
            }
        }
        finally
        {
            lock (Gate)
                _inflight = null;
        }
    }

    private static TimeSpan Backoff(int failures) => failures switch
    {
        1 => TimeSpan.FromSeconds(30),
        2 => TimeSpan.FromMinutes(1),
        3 => TimeSpan.FromMinutes(2),
        4 => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromMinutes(15),
    };

    private static void LoadFromDiskIfNewer()
    {
        try
        {
            DateTime writeTime = File.GetLastWriteTimeUtc(AppPaths.Cache);
            if (writeTime == _diskWriteTime)
                return;
            _diskWriteTime = writeTime;
            if (!File.Exists(AppPaths.Cache))
                return;

            using var stream = File.OpenRead(AppPaths.Cache);
            var file = JsonSerializer.Deserialize(stream, CoreJsonContext.Default.CacheFile);
            if (file != null && (_snapshot is null || file.FetchedAt > _snapshot.FetchedAt))
                _snapshot = new NewsSnapshot(file.Items, file.FetchedAt, IsStale: false);
        }
        catch (Exception ex)
        {
            Log.Error("NewsService", ex);
        }
    }

    private static void SaveToDisk(NewsSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            string tmp = $"{AppPaths.Cache}.{Environment.ProcessId}.tmp";
            using (var stream = File.Create(tmp))
            {
                JsonSerializer.Serialize(
                    stream,
                    new CacheFile { FetchedAt = snapshot.FetchedAt, Items = snapshot.Items.ToList() },
                    CoreJsonContext.Default.CacheFile);
            }
            File.Move(tmp, AppPaths.Cache, overwrite: true);
            lock (Gate)
                _diskWriteTime = File.GetLastWriteTimeUtc(AppPaths.Cache);
        }
        catch (Exception ex)
        {
            Log.Error("NewsService", ex);
        }
    }
}
