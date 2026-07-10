using API.MangaConnectors;
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

    private static HtmlConnectorDefinition LoadMangaKakalotDefinition()
    {
        string json = File.ReadAllText(Path.Join(AppContext.BaseDirectory, "Connectors", "MangaKakalot.json"));
        return JsonSerializer.Deserialize<HtmlConnectorDefinition>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static HtmlConnectorDefinition Definition() => new(
        "Test", "https://example.test/", ["en"], ["example.test"], "https://example.test/icon.png",
        "https://example.test/search?q={query}", "//a", "https://example.test/series/{id}", @"/series/(?<id>[^/]+)",
        new HtmlValueSelector("//h1"), "//a", @"/chapter/(?<id>[^/]+).*?(?<number>\d+)", "//img");
}
