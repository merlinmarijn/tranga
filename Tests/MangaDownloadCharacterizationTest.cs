using System.Reflection;
using System.Xml.Linq;
using API.MangaConnectors;
using API.Schema.MangaContext;
using System.Text.Json;

namespace Tests;

public class MangaDownloadCharacterizationTest
{
    [Fact]
    public void ChapterDownload_UsesCbzAndComicInfoMetadata()
    {
        string libraryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Manga manga = new("Current Manga", "Current description", "https://example.test/cover.jpg",
                MangaReleaseStatus.Continuing, [new Author("Current Author")], [new MangaTag("Current Tag")], [], [],
                new FileLibrary(libraryPath, "Test library"), originalLanguage: "en");
            Chapter chapter = new(manga, "7", 2, "Current chapter");

            Assert.EndsWith(".cbz", chapter.FullArchiveFilePath, StringComparison.OrdinalIgnoreCase);

            MethodInfo method = typeof(Chapter).GetMethod("GetComicInfoXmlString", BindingFlags.Instance | BindingFlags.NonPublic)!;
            XElement comicInfo = XElement.Parse((string)method.Invoke(chapter, null)!);
            Assert.Equal("7", comicInfo.Element("Number")?.Value);
            Assert.Equal("Current chapter", comicInfo.Element("Title")?.Value);
            Assert.Equal("Current Author", comicInfo.Element("Writer")?.Value);
            Assert.Equal("en", comicInfo.Element("LanguageISO")?.Value);
        }
        finally
        {
            if (Directory.Exists(libraryPath))
                Directory.Delete(libraryPath, true);
        }
    }

    [Fact]
    public void ConfiguredHtmlConnectors_DeclareImagePageExtraction()
    {
        string connectorDirectory = Path.Join(AppContext.BaseDirectory, "Connectors");
        HtmlConnectorDefinition[] definitions = Directory.EnumerateFiles(connectorDirectory, "*.json")
            .Select(File.ReadAllText)
            .Select(json => JsonSerializer.Deserialize<HtmlConnectorDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .ToArray();

        Assert.NotEmpty(definitions);
        Assert.All(definitions, definition =>
        {
            Assert.Same(definition, definition.Validate());
            Assert.False(string.IsNullOrWhiteSpace(definition.PageImageXPath));
        });
    }
}
