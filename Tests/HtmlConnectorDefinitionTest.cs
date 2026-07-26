using API.MangaConnectors;
using API.MangaDownloadClients;
using API.Schema.MangaContext;
using HtmlAgilityPack;
using System.Text.Json;

namespace Tests;

public class HtmlConnectorDefinitionTest
{
    [Fact]
    public void Validate_AcceptsTheSmallestUsableDefinition()
    {
        HtmlConnectorDefinition definition = Definition();

        Assert.Same(definition, definition.Validate());
    }

    [Fact]
    public void Validate_RequiresNamedIdAndChapterNumberGroups()
    {
        HtmlConnectorDefinition definition = Definition() with { ChapterRegex = @"chapter/\d+" };

        Assert.Throws<ArgumentException>(definition.Validate);
    }

    [Fact]
    public void Validate_AcceptsBundledMangaKakalotDefinition()
    {
        HtmlConnectorDefinition definition = LoadMangaKakalotDefinition();

        Assert.Same(definition, definition.Validate());
        Assert.Equal("_", definition.SearchQuerySpaceReplacement);
    }

    [Fact]
    public void MangaKakalotSearchSelector_ExcludesCarouselItems()
    {
        HtmlConnectorDefinition definition = LoadMangaKakalotDefinition();
        HtmlDocument document = new();
        document.LoadHtml("<div class='item'><a href='/manga/carousel'><img /></a></div><div class='daily-update'><h3>Keyword : one piece</h3><div class='panel_story_list'><div class='story_item'><a href='/manga/one-piece'><img /></a><h3><a href='/manga/one-piece'>One Piece</a></h3></div></div></div>");

        HtmlNode[] links = document.DocumentNode.SelectNodes(definition.SearchResultXPath)!.ToArray();

        Assert.Equal(["/manga/one-piece"], links.Select(link => link.GetAttributeValue("href", "")));
    }

    [Fact]
    public void MangaKChapterApi_ReturnsTheCompleteChapterList()
    {
        HtmlConnectorDefinition definition = Definition() with
        {
            NextData = true,
            ChapterApiUrl = "https://api.example.test/titles/{id}/chapters",
            ChapterRegex = @"/(?<id>chapter-(?<urlNumber>\d+(?:-\d+)?)).*?Chapter\s+(?<number>\d+(?:-\d+)?)"
        };
        const string mangaPage = """
                                 <script id="__NEXT_DATA__">
                                 {"props":{"pageProps":{"initialManga":{"id":"api-123","chapters":[
                                   {"url":"/series/chapter-3","name":"Chapter 3"}
                                 ]}}}}
                                 </script>
                                 """;
        const string chapterApiResponse = """
                                          {"data":{"chapters":[
                                            {"url":"/series/chapter-18-2","name":"Chapter 18-2"},
                                            {"url":"/series/chapter-18-1","name":"Chapter 18-1"},
                                            {"url":"/series/chapter-18","name":"Chapter 18"}
                                          ]}}
                                          """;
        StubDownloadClient client = new(new Dictionary<string, string>
        {
            ["https://example.test/series"] = mangaPage,
            ["https://api.example.test/titles/api-123/chapters"] = chapterApiResponse
        });
        TestHtmlConnector connector = new(definition, client);
        Manga manga = new("Series", "", "", MangaReleaseStatus.Continuing, [], [], [], []);
        MangaConnectorId<Manga> mangaId = new(manga, connector, "series", "https://example.test/series");

        (Chapter, MangaConnectorId<Chapter>)[] chapters = connector.GetChapters(mangaId);

        Assert.Equal(["18", "18.1", "18.2"], chapters.Select(result => result.Item1.ChapterNumber));
        Assert.Contains("https://api.example.test/titles/api-123/chapters", client.RequestedUrls);
    }

    private static HtmlConnectorDefinition LoadMangaKakalotDefinition()
    {
        string json = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Connectors", "MangaKakalot.json"));
        return JsonSerializer.Deserialize<HtmlConnectorDefinition>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static HtmlConnectorDefinition Definition() => new(
        "Test", "https://example.test/", ["en"], ["example.test"], "https://example.test/icon.png",
        "https://example.test/search?q={query}", "//a", "https://example.test/series/{id}", @"/series/(?<id>[^/]+)",
        new HtmlValueSelector("//h1"), "//a", @"/chapter/(?<id>[^/]+).*?(?<number>\d+)", "//img");

    private sealed class TestHtmlConnector(HtmlConnectorDefinition definition, IDownloadClient client)
        : HtmlMangaConnector(definition, client);

    private sealed class StubDownloadClient(IReadOnlyDictionary<string, string> responses) : IDownloadClient
    {
        public List<string> RequestedUrls { get; } = [];

        public Task<HttpResponseMessage> MakeRequest(string url, RequestType requestType, string? referrer = null,
            CancellationToken? cancellationToken = null)
        {
            RequestedUrls.Add(url);
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = responses.ContainsKey(url)
                    ? System.Net.HttpStatusCode.OK
                    : System.Net.HttpStatusCode.NotFound,
                Content = new StringContent(responses.GetValueOrDefault(url, string.Empty))
            });
        }
    }
}
