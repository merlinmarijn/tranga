using API.Schema.MangaContext;

namespace Tests;

public class ChapterDownloadedCheckTest
{
    [Fact]
    public void CheckDownloadedOnDisk_UsesLoadedChapterWithoutDatabaseRoundTrip()
    {
        string libraryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Manga manga = CreateManga(libraryPath);
            Chapter chapter = new(manga, "7", 2, "Current chapter");
            string archivePath = chapter.FullArchiveFilePath!;
            chapter.FileName = Path.GetFileName(archivePath);
            File.WriteAllText(archivePath, "chapter");

            Assert.True(chapter.CheckDownloadedOnDisk());
            Assert.True(chapter.Downloaded);
            Assert.Equal(Path.GetFileName(archivePath), chapter.FileName);
        }
        finally
        {
            if (Directory.Exists(libraryPath))
                Directory.Delete(libraryPath, true);
        }
    }

    [Fact]
    public void CheckDownloadedOnDisk_ClearsMissingArchive()
    {
        string libraryPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Manga manga = CreateManga(libraryPath);
            Chapter chapter = new(manga, "7", 2, "Current chapter")
            {
                FileName = "missing.cbz",
                Downloaded = true
            };

            Assert.False(chapter.CheckDownloadedOnDisk());
            Assert.False(chapter.Downloaded);
            Assert.Null(chapter.FileName);
        }
        finally
        {
            if (Directory.Exists(libraryPath))
                Directory.Delete(libraryPath, true);
        }
    }

    private static Manga CreateManga(string libraryPath) =>
        new("Current Manga", "Current description", "https://example.test/cover.jpg",
            MangaReleaseStatus.Continuing, [new Author("Current Author")], [new MangaTag("Current Tag")], [], [],
            new FileLibrary(libraryPath, "Test library"), originalLanguage: "en");
}
