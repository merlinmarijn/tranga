using API.MangaConnectors;

namespace Tests;

public class WeebCentralLiveTest
{
    [LiveFact]
    [Trait("Category", "Live")]
    public void DarkMagesReturnToEnlistment_CanBeReadEndToEnd()
    {
        WeebCentral connector = new();

        var searchResults = connector.SearchManga("The Dark Mage's Return to Enlistment");
        var result = Assert.Single(searchResults,
            candidate => candidate.Item1.Name == "The Dark Mage's Return to Enlistment");
        Assert.Equal(3, result.Item1.Authors.Count);
        Assert.Equal(5, result.Item1.MangaTags.Count);
        Assert.Equal((uint)2023, result.Item1.Year);

        var chapters = connector.GetChapters(result.Item2);
        Assert.NotEmpty(chapters);

        string[] imageUrls = connector.GetChapterImageUrls(chapters[^1].Item2);
        Assert.NotEmpty(imageUrls);
        Assert.All(imageUrls, url => Assert.StartsWith("http", url));

        using Stream? firstPage = connector.DownloadImage(imageUrls[0], CancellationToken.None,
            chapters[^1].Item2.WebsiteUrl).GetAwaiter().GetResult();
        Assert.NotNull(firstPage);
        Assert.NotEqual(-1, firstPage.ReadByte());
    }
}

public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TRANGA_RUN_LIVE_CONNECTOR_TESTS") != "1")
            Skip = "Set TRANGA_RUN_LIVE_CONNECTOR_TESTS=1 to run live connector tests.";
    }
}
