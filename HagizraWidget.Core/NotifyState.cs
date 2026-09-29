using System.Text.Json;

namespace HagizraWidget.Core;

/// <summary>Highest flash id already shown as a notification. 0 means "no baseline yet": the first poll only records one.</summary>
public static class NotifyState
{
    public static long LastId
    {
        get
        {
            try
            {
                if (!File.Exists(AppPaths.NotifyState))
                    return 0;
                using var stream = File.OpenRead(AppPaths.NotifyState);
                return JsonSerializer.Deserialize(stream, CoreJsonContext.Default.NotifyStateFile)?.LastId ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    public static void Save(long lastId)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            using var stream = File.Create(AppPaths.NotifyState);
            JsonSerializer.Serialize(stream, new NotifyStateFile { LastId = lastId }, CoreJsonContext.Default.NotifyStateFile);
        }
        catch (Exception ex)
        {
            Log.Error("NotifyState", ex);
        }
    }

    /// <summary>Called when notifications are switched off, so a later re-enable does not replay everything missed meanwhile.</summary>
    public static void Reset()
    {
        try
        {
            File.Delete(AppPaths.NotifyState);
        }
        catch { }
    }
}
