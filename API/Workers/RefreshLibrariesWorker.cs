using System.Diagnostics.CodeAnalysis;
using API.Schema.LibraryContext;
using API.Schema.LibraryContext.LibraryConnectors;
using Microsoft.EntityFrameworkCore;

namespace API.Workers;

public class RefreshLibrariesWorker(IEnumerable<BaseWorker>? dependsOn = null) : BaseWorkerWithContexts(dependsOn)
{
    public static DateTime LastRefresh { get; set; } = DateTime.UnixEpoch;

    public override string Description => "Refresh Komga/Kavita libraries";
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private LibraryContext LibraryContext = null!;

    protected override void SetContexts(IServiceScope serviceScope)
    {
        LibraryContext = GetContext<LibraryContext>(serviceScope);
    }
    
    protected override async Task<BaseWorker[]> DoWorkInternal()
    {
        Log.Debug("Refreshing libraries...");
        LastRefresh = DateTime.UtcNow;
        List<LibraryConnector> libraries = await LibraryContext.LibraryConnectors.ToListAsync(CancellationToken);
        for (int i = 0; i < libraries.Count; i++)
        {
            SetStatus($"Refreshing {libraries[i].LibraryType} at {libraries[i].BaseUrl}", i, libraries.Count);
            await libraries[i].UpdateLibrary(CancellationToken);
        }
        Log.Debug("Libraries Refreshed...");
        return [];
    }
}

public enum LibraryRefreshSetting
{
    /// <summary>
    /// Refresh Libraries after all Manga are downloaded
    /// </summary>
    AfterAllFinished,
    /// <summary>
    /// Refresh Libraries after a Manga is downloaded
    /// </summary>
    AfterMangaFinished,
    /// <summary>
    /// Refresh Libraries after every download
    /// </summary>
    AfterEveryChapter,
    /// <summary>
    /// Refresh Libraries while downloading chapters, every x minutes
    /// </summary>
    WhileDownloading
}