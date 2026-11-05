using System.Diagnostics.CodeAnalysis;
using API.Schema.MangaContext;
using API.Workers.MangaDownloadWorkers;
using Microsoft.EntityFrameworkCore;

namespace API.Workers.PeriodicWorkers;

/// <summary>
/// Creates Jobs to update available Chapters for all Manga that are marked for Download
/// </summary>
public class CheckForNewChaptersWorker(TimeSpan? interval = null, IEnumerable<BaseWorker>? dependsOn = null)
    : BaseWorkerWithContexts(dependsOn), IPeriodic
{
    public DateTime LastExecution { get; set; } = DateTime.UnixEpoch;
    public TimeSpan Interval { get; set; } = interval??Constants.CheckForNewChaptersInterval;
    
    private static readonly Dictionary<string, DateTime> FinishedLastChecked = new();

    public override string Description => "Check for new chapters";

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private MangaContext MangaContext = null!;

    protected override void SetContexts(IServiceScope serviceScope)
    {
        MangaContext = GetContext<MangaContext>(serviceScope);
    }
    
    protected override async Task<BaseWorker[]> DoWorkInternal()
    {
        Log.Debug("Checking for new chapters...");
        SetStatus("Loading monitored series");
        List<MangaConnectorId<Manga>> connectorIdsManga = await MangaContext.MangaConnectorToManga
            .Include(id => id.Obj)
            .Where(id => id.UseForDownload)
            .ToListAsync(CancellationToken);
        // Finished series (completed/cancelled, every chapter downloaded) are only re-checked occasionally, to catch extras
        HashSet<string> finishedMangaIds = (await MangaContext.Mangas
            .Where(m => (m.ReleaseStatus == MangaReleaseStatus.Completed || m.ReleaseStatus == MangaReleaseStatus.Cancelled)
                        && m.Chapters.Any() && m.Chapters.All(c => c.Downloaded))
            .Select(m => m.Key)
            .ToListAsync(CancellationToken)).ToHashSet();
        DateTime now = DateTime.UtcNow;
        connectorIdsManga.RemoveAll(id =>
        {
            if (!finishedMangaIds.Contains(id.ObjId)) return false;
            // ponytail: in-memory, so each finished series gets one check after restart; persist if that matters
            if (FinishedLastChecked.TryGetValue(id.Key, out DateTime last) && now - last < Constants.FinishedSeriesCheckInterval) return true;
            FinishedLastChecked[id.Key] = now;
            return false;
        });
        Log.DebugFormat("Creating {0} update jobs...", connectorIdsManga.Count);
        SetStatus($"Queueing chapter list refresh for {connectorIdsManga.Count} series");

        List<BaseWorker> newWorkers = connectorIdsManga.Select(id => new RetrieveMangaChaptersFromMangaconnectorWorker(id, Tranga.Settings.DownloadLanguage))
            .ToList<BaseWorker>();

        return newWorkers.ToArray();
    }
}