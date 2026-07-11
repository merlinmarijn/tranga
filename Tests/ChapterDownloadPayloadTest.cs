using API;
using API.MangaConnectors;
using API.MangaDownloadClients;
using API.Schema.MangaContext;
using API.Workers.MangaDownloadWorkers;
using System.IO.Compression;

namespace Tests;

public class ChapterDownloadPayloadTest
{
    [Fact]
    public void MangaConnector_ReturnsImagePagesPayload()
    {
        ImagePagesPayload payload = Assert.IsType<ImagePagesPayload>(new ImageConnector().GetChapterPayload(null!));

        Assert.Equal(["https://example.test/1.jpg"], payload.Urls);
    }

    [Fact]
    public void NovelConnector_CanReturnChapterHtmlPayload()
    {
        ChapterHtmlPayload payload = Assert.IsType<ChapterHtmlPayload>(new HtmlConnector().GetChapterPayload(null!));

        Assert.Equal("<p>Chapter text</p>", payload.Html);
    }

    [Fact]
    public async Task ImagePagesPayload_ExportsTheExistingCbzShape()
    {
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.cbz");
        try
        {
            Manga manga = new("Test", "", "", MangaReleaseStatus.Unreleased, [], [], [], []);
            Chapter chapter = new(manga, "1", null);

            Assert.True(await ChapterExporter.Export(new ImagePagesPayload(["https://example.test/1.png"]),
                new ImageConnector(), chapter, path, null, () => Task.CompletedTask, CancellationToken.None));

            using ZipArchive archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("0.jpg"));
            Assert.Equal(Constants.CreateComicInfoXml, archive.GetEntry("ComicInfo.xml") is not null);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ChapterHtmlPayload_ExportsAnEpubWithMetadataAndText()
    {
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid()}.epub");
        try
        {
            Manga manga = new("Novel", "Description", "", MangaReleaseStatus.Unreleased, [new Author("Author")], [], [], [],
                originalLanguage: "en", contentKind: ContentKind.Novel);
            Chapter chapter = new(manga, "2", null, "Chapter title");

            Assert.True(await ChapterExporter.Export(new ChapterHtmlPayload("<script>advertisement()</script><p>First paragraph.</p><p><strong>Formatted</strong> second paragraph.</p>"), new ImageConnector(), chapter,
                path, null, () => Task.CompletedTask, CancellationToken.None));

            using ZipArchive archive = ZipFile.OpenRead(path);
            ZipArchiveEntry mimetype = Assert.Single(archive.Entries, entry => entry.FullName == "mimetype");
            Assert.Equal(mimetype.Length, mimetype.CompressedLength);
            Assert.NotNull(archive.GetEntry("META-INF/container.xml"));
            Assert.NotNull(archive.GetEntry("OEBPS/content.opf"));
            Assert.NotNull(archive.GetEntry("OEBPS/chapter.xhtml"));
            using StreamReader reader = new(archive.GetEntry("OEBPS/content.opf")!.Open());
            string package = await reader.ReadToEndAsync();
            Assert.Contains("Author", package);
            Assert.Contains("Description", package);
            Assert.Contains("calibre:series", package);
            Assert.Contains(">Novel</", package);
            Assert.Contains("calibre:series_index", package);
            Assert.Contains(">2</", package);
            Assert.Contains("belongs-to-collection", package);
            string chapterHtml = await new StreamReader(archive.GetEntry("OEBPS/chapter.xhtml")!.Open()).ReadToEndAsync();
            Assert.Contains("Chapter title", chapterHtml);
            Assert.Contains("First paragraph.", chapterHtml);
            Assert.Contains("Formatted", chapterHtml);
            Assert.DoesNotContain("advertisement", chapterHtml);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(chapterHtml, "<p").Count);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task NovelChapters_AreCombinedInChapterOrder()
    {
        string libraryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Manga manga = new("Novel", "Description", "", MangaReleaseStatus.Unreleased, [], [], [], [],
                new FileLibrary(libraryPath, "Test library"), contentKind: ContentKind.Novel);
            Chapter first = new(manga, "1", null, "First");
            Chapter twoHundred = new(manga, "200", null, "Two hundred");
            Chapter middle = new(manga, "101", null, "Middle");
            manga.Chapters = [first, twoHundred, middle];
            Assert.EndsWith(".tranga", first.FullArchiveFilePath, StringComparison.OrdinalIgnoreCase);

            List<Chapter> downloaded = [];
            foreach ((Chapter chapter, string contents) in new[]
                     {
                         (first, "<p>First text</p>"), (twoHundred, "<p>Last text</p>"), (middle, "<p>Middle text</p>")
                     })
            {
                Assert.True(await ChapterExporter.Export(new ChapterHtmlPayload(contents), new ImageConnector(), chapter,
                    chapter.FullArchiveFilePath!, null, () => Task.CompletedTask, CancellationToken.None));
                if (chapter == first)
                {
                    string sourcePath = Assert.IsType<string>(chapter.FullArchiveFilePath);
                    string legacyPath = Path.ChangeExtension(sourcePath, ".epub");
                    File.Move(sourcePath, legacyPath);
                    chapter.FileName = Path.GetFileName(legacyPath);
                }
                downloaded.Add(chapter);
                Assert.True(await ChapterExporter.ExportNovelSeries(manga, downloaded, CancellationToken.None));
                Assert.Equal(chapter == first ? 1 : 0, ChapterExporter.RenameLegacyNovelSources(downloaded));
            }

            using ZipArchive archive = ZipFile.OpenRead(Path.Join(manga.FullDirectoryPath, "Complete.epub"));
            string navigation = await new StreamReader(archive.GetEntry("OEBPS/nav.xhtml")!.Open()).ReadToEndAsync();
            Assert.True(navigation.IndexOf("First", StringComparison.Ordinal) < navigation.IndexOf("Middle", StringComparison.Ordinal));
            Assert.True(navigation.IndexOf("Middle", StringComparison.Ordinal) < navigation.IndexOf("Two hundred", StringComparison.Ordinal));
            Assert.Contains("Middle text", await new StreamReader(archive.GetEntry($"OEBPS/chapters/{middle.Key}.xhtml")!.Open()).ReadToEndAsync());
        }
        finally
        {
            if (Directory.Exists(libraryPath))
                Directory.Delete(libraryPath, true);
        }
    }

    private class ImageConnector : MangaConnector
    {
        public ImageConnector() : base("Image test", ["en"], ["example.test"], "https://example.test/icon.png")
        {
            downloadClient = new ImageDownloadClient();
        }

        public override (Manga, MangaConnectorId<Manga>)[] SearchManga(string mangaSearchName) => [];
        public override (Manga, MangaConnectorId<Manga>)? GetMangaFromUrl(string url) => null;
        public override (Manga, MangaConnectorId<Manga>)? GetMangaFromId(string mangaIdOnSite) => null;
        public override (Chapter, MangaConnectorId<Chapter>)[] GetChapters(MangaConnectorId<Manga> mangaId, string? language = null) => [];
        internal override string[] GetChapterImageUrls(MangaConnectorId<Chapter> chapterId) => ["https://example.test/1.jpg"];
    }

    private sealed class HtmlConnector : ImageConnector
    {
        internal override ChapterDownloadPayload GetChapterPayload(MangaConnectorId<Chapter> chapterId) =>
            new ChapterHtmlPayload("<p>Chapter text</p>");
    }

    private sealed class ImageDownloadClient : IDownloadClient
    {
        private static readonly byte[] ImageBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL7nQAAAABJRU5ErkJggg==");

        public Task<HttpResponseMessage> MakeRequest(string url, RequestType requestType, string? referrer = null,
            CancellationToken? cancellationToken = null) => Task.FromResult(new HttpResponseMessage
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            Content = new ByteArrayContent(ImageBytes)
        });
    }
}
