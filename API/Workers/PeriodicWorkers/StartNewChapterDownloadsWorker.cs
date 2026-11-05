using System.Diagnostics.CodeAnalysis;
using API.Schema.ActionsContext;
using API.Schema.ActionsContext.Actions;
using API.Schema.MangaContext;
using API.Workers.MangaDownloadWorkers;
using Microsoft.EntityFrameworkCore;

namespace API.Workers.PeriodicWorkers;

/// <summary>
/// Create new Workers for Chapters on Manga marked for Download, that havent been downloaded yet.
/// </summary>
public class StartNewChapterDownloadsWorker(TimeSpan? interval = null, IEnumerable<BaseWorker>? dependsOn = null)
    : BaseWorkerWithContexts(dependsOn), IPeriodic
{

    public DateTime LastExecution { get; set; } = DateTime.UnixEpoch;
    public TimeSpan Interval { get; set; } = interval ?? TimeSpan.FromMinutes(1);
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private MangaContext MangaContext = null!;
    private ActionsContext ActionsContext = null!;
    private static bool _backoffSeeded;

    public override string Description => "Start new chapter downloads";

    protected override void SetContexts(IServiceScope serviceScope)
    {
        MangaContext = GetContext<MangaContext>(serviceScope);
        ActionsContext = GetContext<ActionsContext>(serviceScope);
    }

    private async Task SeedBackoffOnce()
    {
        if (_backoffSeeded) return;
        _backoffSeeded = true;
        // Only failures after the chapter's last success count
        var failed = await ActionsContext.Actions.OfType<ChapterDownloadFailedActionRecord>()
            .GroupBy(a => a.ChapterId)
            .Select(g => new { ChapterId = g.Key, Times = g.Select(a => a.PerformedAt).ToList() })
            .ToListAsync(CancellationToken);
        Dictionary<string, DateTime> lastSuccess = await ActionsContext.Actions.OfType<ChapterDownloadedActionRecord>()
            .GroupBy(a => a.ChapterId)
            .Select(g => new { ChapterId = g.Key, Last = g.Max(a => a.PerformedAt) })
            .ToDictionaryAsync(x => x.ChapterId, x => x.Last, CancellationToken);
        DownloadChapterFromMangaconnectorWorker.SeedBackoff(failed.Select(f =>
        {
            DateTime since = lastSuccess.GetValueOrDefault(f.ChapterId, DateTime.MinValue);
            List<DateTime> recent = f.Times.Where(t => t > since).ToList();
            return (f.ChapterId, recent.Count, recent.Count > 0 ? recent.Max() : DateTime.MinValue);
        }));
    }
    
    protected override async Task<BaseWorker[]> DoWorkInternal()
    {
        Log.Debug("Checking for missing chapters...");
        SetStatus("Looking for missing chapters");
        await SeedBackoffOnce();
        
        // Get missing chapters
        List<MangaConnectorId<Chapter>> missingChapters = await GetMissingChapters(MangaContext, CancellationToken);
        
        Log.DebugFormat("Found {0} missing chapters.", missingChapters.Count);
        // Queued workers count too, otherwise every tick re-queues the same chapters once concurrency is maxed
        HashSet<string> chaptersDownloading = Tranga.GetKnownWorkers()
            .OfType<DownloadChapterFromMangaconnectorWorker>()
            .Select(w => w.ChapterIdId).ToHashSet();
        missingChapters.RemoveAll(ch => chaptersDownloading.Contains(ch.Key) || DownloadChapterFromMangaconnectorWorker.IsBackingOff(ch.ObjId));
        Log.DebugFormat("{0} chapter not being downloaded", missingChapters.Count);
        
        // Maximum Concurrent workers
        int downloadWorkers = Tranga.GetRunningWorkers().Count(w => w.GetType() == typeof(DownloadChapterFromMangaconnectorWorker));
        int amountNewWorkers = Math.Min(Tranga.Settings.MaxConcurrentDownloads, Tranga.Settings.MaxConcurrentDownloads - downloadWorkers);
        
        Log.DebugFormat("{0} running download Workers. {1} available new download Workers.", downloadWorkers, amountNewWorkers);
        IEnumerable<MangaConnectorId<Chapter>> newDownloadChapters = missingChapters.OrderBy(ch => ch.Obj, new Chapter.ChapterComparer()).Take(amountNewWorkers);

        // Create new jobs
        List<BaseWorker> newWorkers = newDownloadChapters.Select(mcId => new DownloadChapterFromMangaconnectorWorker(mcId)).ToList<BaseWorker>();
        SetStatus($"Starting {newWorkers.Count} of {missingChapters.Count} missing chapters");
        
        return newWorkers.ToArray();
    }
    
    internal static async Task<List<MangaConnectorId<Chapter>>> GetMissingChapters(MangaContext ctx, CancellationToken cancellationToken) => await ctx.MangaConnectorToChapter
        .Include(id => id.Obj)
        .Where(id => !id.Obj.Downloaded && id.UseForDownload)
        .ToListAsync(cancellationToken);
}