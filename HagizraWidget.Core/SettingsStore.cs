using System.Text.Json;

namespace HagizraWidget.Core;

public static class SettingsStore
{
    private static readonly object Gate = new();
    private static WidgetSettings? _cached;
    private static DateTime _cachedWriteTime;

    /// <summary>Returns the settings, re-reading the file only when it changed since the last load (other processes may write it).</summary>
    public static WidgetSettings Load()
    {
        lock (Gate)
        {
            try
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(AppPaths.Settings);
                if (_cached != null && writeTime == _cachedWriteTime)
                    return _cached;

                _cachedWriteTime = writeTime;
                if (File.Exists(AppPaths.Settings))
                {
                    using var stream = File.OpenRead(AppPaths.Settings);
                    _cached = JsonSerializer.Deserialize(stream, CoreJsonContext.Default.WidgetSettings);
                }
            }
            catch (Exception ex)
            {
                Log.Error("SettingsStore", ex);
            }

            return _cached ??= new WidgetSettings();
        }
    }

    public static void Save(WidgetSettings settings)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(AppPaths.Root);
            string tmp = AppPaths.Settings + ".tmp";
            using (var stream = File.Create(tmp))
                JsonSerializer.Serialize(stream, settings, CoreJsonContext.Default.WidgetSettings);
            File.Move(tmp, AppPaths.Settings, overwrite: true);

            _cached = settings;
            _cachedWriteTime = File.GetLastWriteTimeUtc(AppPaths.Settings);
        }
    }
}
