namespace HagizraWidget.Core;

public static class Log
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static void Error(string source, Exception ex) => Write(source, $"{ex.GetType().Name}: {ex.Message}");

    public static void Info(string source, string message) => Write(source, message);

    private static void Write(string source, string message)
    {
        try
        {
            lock (Gate)
            {
                var info = new FileInfo(AppPaths.LogFile);
                if (!info.Exists)
                    Directory.CreateDirectory(info.DirectoryName!);
                else if (info.Length > MaxBytes)
                    File.WriteAllText(info.FullName, "");

                File.AppendAllText(info.FullName, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {message}\n");
            }
        }
        catch { }
    }
}
