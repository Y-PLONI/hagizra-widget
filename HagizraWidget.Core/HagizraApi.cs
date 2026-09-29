using System.Globalization;
using System.Net;
using System.Text.Json;

namespace HagizraWidget.Core;

public static class HagizraApi
{
    public const string SiteUrl = "https://hagizra.news/";

    // One pooled connection that is released shortly after use, so no socket stays open between refreshes.
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 1,
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HagizraWidget/1.0");
        return client;
    }

    public static async Task<IReadOnlyList<NewsItem>> FetchAsync(int limit, CancellationToken ct = default)
    {
        string url = $"{SiteUrl}api/messages?limit={limit.ToString(CultureInfo.InvariantCulture)}&offset=0";
        using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return Parse(doc.RootElement);
    }

    /// <summary>Accepts a bare array or an object holding it under "messages"; unknown fields are ignored.</summary>
    public static IReadOnlyList<NewsItem> Parse(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("messages", out var inner))
            root = inner;
        if (root.ValueKind != JsonValueKind.Array)
            throw new FormatException("Unexpected messages payload");

        var items = new List<NewsItem>(root.GetArrayLength());
        foreach (var m in root.EnumerateArray())
        {
            if (m.ValueKind != JsonValueKind.Object || GetBool(m, "deleted"))
                continue;
            if (!m.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out long id))
                continue;

            var (title, body, media) = NewsNormalizer.Normalize(GetString(m, "text"));
            if (title.Length == 0 && body.Length == 0 && media == MediaKind.None)
                continue;

            items.Add(new NewsItem(
                id,
                ParseTime(GetString(m, "timestamp")),
                title,
                body,
                media,
                IsBreaking: m.TryGetProperty("breakingId", out var b) && b.ValueKind == JsonValueKind.Number && b.GetInt64() > 0,
                IsAd: GetBool(m, "is_ads"),
                IsSilent: GetBool(m, "silent")));
        }

        items.Sort((a, b) => a.Time != b.Time ? b.Time.CompareTo(a.Time) : b.Id.CompareTo(a.Id));
        return items;
    }

    private static DateTimeOffset ParseTime(string? s) =>
        DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t) ? t : DateTimeOffset.UtcNow;

    private static string? GetString(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
