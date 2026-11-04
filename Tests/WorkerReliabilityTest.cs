using API.Schema.MangaContext;
using API.Workers.MangaDownloadWorkers;

namespace Tests;

public class WorkerReliabilityTest
{
    [Fact]
    public void ChapterCompareTo_IsSymmetricWithMixedVolumes()
    {
        Manga manga = new("Test", "", "", MangaReleaseStatus.Continuing, [], [], [], []);
        Chapter withVolume = new(manga, "5", 1);
        Chapter withoutVolume = new(manga, "3", null);

        Assert.Equal(-withVolume.CompareTo(withoutVolume), withoutVolume.CompareTo(withVolume));
        Assert.True(withVolume.CompareTo(withoutVolume) > 0); // falls back to chapter number
        Assert.True(new Chapter(manga, "9", 1).CompareTo(new Chapter(manga, "1", 2)) < 0); // volume wins when both set
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(4, 40)]
    [InlineData(20, 1440)]
    public void DownloadFailureBackoff_DoublesAndCaps(int failures, double minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), DownloadChapterFromMangaconnectorWorker.BackoffFor(failures));
}

public class BackoffSeedTest
{
    [Fact]
    public void SeedBackoff_RestoresRecentFailuresAndIgnoresExpired()
    {
        DownloadChapterFromMangaconnectorWorker.SeedBackoff([
            ("seed-recent", 3, DateTime.UtcNow.AddMinutes(-1)),   // 20 min backoff, still active
            ("seed-expired", 1, DateTime.UtcNow.AddHours(-1)),    // 5 min backoff, long over
            ("seed-none", 0, DateTime.UtcNow),
        ]);
        Assert.True(DownloadChapterFromMangaconnectorWorker.IsBackingOff("seed-recent"));
        Assert.False(DownloadChapterFromMangaconnectorWorker.IsBackingOff("seed-expired"));
        Assert.False(DownloadChapterFromMangaconnectorWorker.IsBackingOff("seed-none"));
    }
}
