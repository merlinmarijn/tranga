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

            Assert.True(await ChapterExporter.Export(new ChapterHtmlPayload("<p>Novel text</p>"), new ImageConnector(), chapter,
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
            Assert.Contains("Chapter title", await new StreamReader(archive.GetEntry("OEBPS/chapter.xhtml")!.Open()).ReadToEndAsync());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
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
