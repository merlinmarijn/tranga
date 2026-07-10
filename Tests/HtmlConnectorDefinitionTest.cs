using API.MangaConnectors;

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

    private static HtmlConnectorDefinition Definition() => new(
        "Test", "https://example.test/", ["en"], ["example.test"], "https://example.test/icon.png",
        "https://example.test/search?q={query}", "//a", "https://example.test/series/{id}", @"/series/(?<id>[^/]+)",
        new HtmlValueSelector("//h1"), "//a", @"/chapter/(?<id>[^/]+).*?(?<number>\d+)", "//img");
}
