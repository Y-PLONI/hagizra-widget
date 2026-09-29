using System.Text.Json.Serialization;

namespace HagizraWidget.Core;

[Flags]
public enum MediaKind
{
    None = 0,
    Image = 1,
    Video = 2,
    Audio = 4,
}

/// <summary>One flash, already stripped of markdown, HTML and media markers.</summary>
public sealed record NewsItem(
    long Id,
    DateTimeOffset Time,
    string Title,
    string Body,
    MediaKind Media,
    bool IsBreaking,
    bool IsAd,
    bool IsSilent);

public sealed record NewsSnapshot(IReadOnlyList<NewsItem> Items, DateTimeOffset FetchedAt, bool IsStale);

public sealed class WidgetSettings
{
    public bool Notifications { get; set; }
    public bool ShowAds { get; set; }
}

internal sealed class CacheFile
{
    public DateTimeOffset FetchedAt { get; set; }
    public List<NewsItem> Items { get; set; } = new();
}

internal sealed class NotifyStateFile
{
    public long LastId { get; set; }
}

[JsonSerializable(typeof(WidgetSettings))]
[JsonSerializable(typeof(CacheFile))]
[JsonSerializable(typeof(NotifyStateFile))]
internal sealed partial class CoreJsonContext : JsonSerializerContext { }
