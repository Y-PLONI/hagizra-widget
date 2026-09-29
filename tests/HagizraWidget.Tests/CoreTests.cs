using System.Text.Json;
using HagizraWidget.Core;
using Xunit;

namespace HagizraWidget.Tests;

public class NewsNormalizerTests
{
    [Fact]
    public void SplitsTitleFromBody()
    {
        var (title, body, media) = NewsNormalizer.Normalize("**קמפיין בחירות |** ראש הממשלה הגיע כעת לאשדוד.");
        Assert.Equal("קמפיין בחירות", title);
        Assert.Equal("ראש הממשלה הגיע כעת לאשדוד.", body);
        Assert.Equal(MediaKind.None, media);
    }

    [Fact]
    public void ExtractsMediaMarkersAndDropsTheUrl()
    {
        var (title, body, media) = NewsNormalizer.Normalize(
            "**חברון שלנו |** ערוצי האויב מתבכיינים.\n[video-embedded#](https://stream.hagizra.news/a/v.mp4#720x1280)\n[image-embedded#](https://stream.hagizra.news/b/image.png#328x61:)");
        Assert.Equal("חברון שלנו", title);
        Assert.Equal("ערוצי האויב מתבכיינים.", body);
        Assert.Equal(MediaKind.Video | MediaKind.Image, media);
    }

    [Fact]
    public void StripsHtmlSpansAndQuadrupleAsterisks()
    {
        var (title, body, _) = NewsNormalizer.Normalize(
            "**<span style=\"color: #4caf50\">פסק הלכה</span> <span style=\"color: #4caf50\">|**</span> <span>אפשר לצאת ידי חובה.</span> (תורת לחימה)");
        Assert.Equal("פסק הלכה", title);
        Assert.Equal("אפשר לצאת ידי חובה. (תורת לחימה)", body);

        (title, _, _) = NewsNormalizer.Normalize("****טירוף בפסטלה |**** סוכה כשרה.");
        Assert.Equal("טירוף בפסטלה", title);
    }

    [Fact]
    public void KeepsLinkTextAndNeverLeavesHtmlBehind()
    {
        var (_, body, _) = NewsNormalizer.Normalize("ראו [כאן](https://example.com/x) <script>alert(1)</script> &amp; עוד");
        Assert.Equal("ראו כאן alert(1) & עוד", body);
    }

    [Fact]
    public void MediaOnlyMessageHasNoText()
    {
        var (title, body, media) = NewsNormalizer.Normalize("[image-embedded#](https://stream.hagizra.news/x/y.jpg#1x1:)");
        Assert.Equal("", title);
        Assert.Equal("", body);
        Assert.Equal(MediaKind.Image, media);
    }
}

public class HagizraApiTests
{
    private static IReadOnlyList<NewsItem> LoadFixture()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "messages.json")));
        return HagizraApi.Parse(doc.RootElement);
    }

    [Fact]
    public void ParsesLiveSampleNewestFirst()
    {
        var items = LoadFixture();
        Assert.Equal(40, items.Count);
        Assert.True(items.Zip(items.Skip(1)).All(p => p.First.Time >= p.Second.Time));
        Assert.All(items, i => Assert.DoesNotContain("embedded#", i.Body));
        Assert.All(items, i => Assert.DoesNotContain("<span", i.Body));
        Assert.Equal(2300770, items[0].Id);
        Assert.Equal(new DateTimeOffset(2026, 9, 29, 11, 11, 28, TimeSpan.Zero).Ticks / TimeSpan.TicksPerSecond, items[0].Time.Ticks / TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void FlagsAdsAndBreakingNews()
    {
        var items = LoadFixture();
        Assert.Equal(3, items.Count(i => i.IsAd));
        Assert.Single(items, i => i.IsBreaking);
    }

    [Fact]
    public void AcceptsWrappedObjectAndSkipsDeleted()
    {
        using var doc = JsonDocument.Parse("""{"messages":[{"id":2,"text":"a","timestamp":"2026-01-01T00:00:00Z"},{"id":1,"text":"b","deleted":true}]}""");
        var items = HagizraApi.Parse(doc.RootElement);
        Assert.Equal(2, Assert.Single(items).Id);
    }
}
