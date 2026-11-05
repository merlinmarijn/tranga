using System.Diagnostics.CodeAnalysis;
using API.Schema.ActionsContext;
using API.Schema.ActionsContext.Actions;
using API.Schema.MangaContext;
using API.Schema.MangaContext.MetadataFetchers;
using Microsoft.EntityFrameworkCore;

namespace API.Workers.PeriodicWorkers;

/// <summary>
/// Updates Metadata for all Manga
/// </summary>
/// <param name="interval"></param>
/// <param name="dependsOn"></param>
public class UpdateMetadataWorker(TimeSpan? interval = null, IEnumerable<BaseWorker>? dependsOn = null)
    : BaseWorkerWithContexts(dependsOn), IPeriodic
{

    public DateTime LastExecution { get; set; } = DateTime.UnixEpoch;
    public TimeSpan Interval { get; set; } = interval ?? TimeSpan.FromHours(12);

    public override string Description => "Update metadata";

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private MangaContext MangaContext = null!;
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private ActionsContext ActionsContext = null!;

    protected override void SetContexts(IServiceScope serviceScope)
    {
        MangaContext = GetContext<MangaContext>(serviceScope);
        ActionsContext = GetContext<ActionsContext>(serviceScope);
    }
    
    protected override async Task<BaseWorker[]> DoWorkInternal()
    {
        Log.Debug("Updating metadata...");
        SetStatus("Loading metadata entries");
        // Get MetadataEntries of Manga marked for download
        List<MetadataEntry> metadataEntriesToUpdate = await MangaContext.MangaConnectorToManga
            .Where(m => m.UseForDownload) // Get marked Manga
            .Join(
                MangaContext.MetadataEntries.Include(e => e.Manga),
                mcId => mcId.ObjId,
                e => e.MangaId,
                (mcId, e) => e) // return MetadataEntry
            .ToListAsync(CancellationToken);
        Log.DebugFormat("Updating metadata of {0} manga...", metadataEntriesToUpdate.Count);

        for (int i = 0; i < metadataEntriesToUpdate.Count; i++)
        {
            MetadataEntry metadataEntry = metadataEntriesToUpdate[i];
            SetStatus($"Updating metadata for {metadataEntry.Manga.Name} ({i + 1}/{metadataEntriesToUpdate.Count})", i, metadataEntriesToUpdate.Count);
            Log.DebugFormat("Updating metadata of {0}...", metadataEntry);
            if(Tranga.MetadataFetchers.FirstOrDefault(f => f.Name == metadataEntry.MetadataFetcherName) is not { } fetcher)
                continue;
            await fetcher.UpdateMetadata(metadataEntry, MangaContext, CancellationToken);
            ActionsContext.Actions.Add(new MetadataUpdatedActionRecord(metadataEntry.Manga, fetcher));
        }
        Log.Debug("Updated metadata.");
        SetStatus("Saving");

        if(await MangaContext.Sync(CancellationToken, GetType(), System.Reflection.MethodBase.GetCurrentMethod()?.Name) is { success: false } e)
            Log.ErrorFormat("Failed to save database changes: {0}", e.exceptionMessage);
        
        if(await ActionsContext.Sync(CancellationToken, GetType(), "Metadata Updated") is { success: false } actionsContextException)
            Log.ErrorFormat("Failed to save database changes: {0}", actionsContextException.exceptionMessage);

        return [];
    }
}