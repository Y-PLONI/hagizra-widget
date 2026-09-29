namespace HagizraWidget.Core;

public static class AppPaths
{
    /// <summary>Overridable so tests never touch the real data folder.</summary>
    public static string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HagizraWidget");

    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Cache => Path.Combine(Root, "cache.json");
    public static string NotifyState => Path.Combine(Root, "notify-state.json");
    public static string LogFile => Path.Combine(Root, "log.txt");
}
