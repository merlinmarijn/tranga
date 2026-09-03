using System.Text.Json;
using API.MangaConnectors;

namespace Tests;

public class MangaKLiveTest
{
    [LiveFact]
    [Trait("Category", "Live")]
    public void DarkMagesReturnToEnlistment_CanBeReadEndToEnd()
    {
        HtmlConnectorDefinition definition = JsonSerializer.Deserialize<HtmlConnectorDefinition>(
            File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Connectors", "MangaK.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        TestHtmlConnector connector = new(definition);

        var result = Assert.Single(connector.SearchManga("The Dark Mage's Return to Enlistment"),
            candidate => candidate.Item1.Name.Contains("Dark Mage", StringComparison.OrdinalIgnoreCase));

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

    private sealed class TestHtmlConnector(HtmlConnectorDefinition definition) : HtmlMangaConnector(definition);
}
