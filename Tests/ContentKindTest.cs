using API.MangaConnectors;
using API.Schema.MangaContext;
using ConnectorDto = API.Controllers.DTOs.MangaConnector;

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

    [Fact]
    public void ConnectorDtosExposeTheirContentKind()
    {
        MangaConnector connector = new LightNovelWorld();
        ConnectorDto dto = new(connector.Name, connector.Enabled, connector.IconUrl, connector.SupportedLanguages, connector.ContentKind);

        Assert.Equal(ContentKind.Novel, dto.ContentKind);
    }
}
