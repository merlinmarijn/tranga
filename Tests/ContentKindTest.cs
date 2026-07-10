using API.MangaConnectors;
using API.Schema.MangaContext;

namespace Tests;

public class ContentKindTest
{
    [Fact]
    public void ExistingConnectorsAndNewSeriesDefaultToManga()
    {
        Manga manga = new("Test", "", "", MangaReleaseStatus.Unreleased, [], [], [], []);
        MangaConnector[] connectors = [new Global(), new AsuraComic(), new MangaDex(), new Mangaworld(), new WeebCentral(),
            .. MangaConnectorCatalog.LoadConfigured()];

        Assert.Equal(ContentKind.Manga, manga.ContentKind);
        Assert.All(connectors, connector => Assert.Equal(ContentKind.Manga, connector.ContentKind));
    }
}
