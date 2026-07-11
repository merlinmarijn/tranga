using System.Diagnostics.CodeAnalysis;
using API.MangaConnectors;
using API.Schema.ActionsContext;
using API.Schema.ActionsContext.Actions;
using API.Schema.MangaContext;
using API.Schema.NotificationsContext;
using API.Workers.PeriodicWorkers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace API.Workers.MangaDownloadWorkers;

/// <summary>
/// Downloads single chapter for Manga from Mangaconnector
/// </summary>
/// <param name="chId"></param>
/// <param name="dependsOn"></param>
public class DownloadChapterFromMangaconnectorWorker(MangaConnectorId<Chapter> chId, IEnumerable<BaseWorker>? dependsOn = null)
    : BaseWorkerWithContexts(dependsOn)
{
    public readonly string ChapterIdId = chId.Key;

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private MangaContext MangaContext = null!;
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private ActionsContext ActionsContext = null!;
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private NotificationsContext NotificationsContext = null!;

    protected override void SetContexts(IServiceScope serviceScope)
    {
        MangaContext = GetContext<MangaContext>(serviceScope);
        ActionsContext = GetContext<ActionsContext>(serviceScope);
        NotificationsContext = GetContext<NotificationsContext>(serviceScope);
    }
    
    protected override async Task<BaseWorker[]> DoWorkInternal()
    {
        Log.Debug($"Downloading chapter for MangaConnectorId {ChapterIdId}...");
        // Getting MangaConnector info
        if (await MangaContext.MangaConnectorToChapter
                .Include(id => id.Obj)
                .ThenInclude(c => c.ParentManga)
                .ThenInclude(m => m.Library)
                .FirstOrDefaultAsync(c => c.Key == ChapterIdId, CancellationToken) is not { } mangaConnectorId)
        {
            Log.Error("Could not get MangaConnectorId.");
            return [];
        }
        
        // Check if Chapter already exists...
        if (await mangaConnectorId.Obj.CheckDownloaded(MangaContext, CancellationToken))
        {
            Log.Warn("Chapter already exists!");
            return [];
        }
        
        if (!Tranga.TryGetMangaConnector(mangaConnectorId.MangaConnectorName, out MangaConnector? mangaConnector))
        {
            Log.Error("Could not get MangaConnector.");
            return [];
        }
        
        Log.Debug($"Downloading chapter for MangaConnectorId {mangaConnectorId}...");
        
        Chapter chapter = mangaConnectorId.Obj;
        if (chapter.ParentManga.LibraryId is null)
        {
            Log.Info($"Library is not set for {chapter.ParentManga} {chapter}");
            return [];
        }
        
        Log.Info($"Getting chapter payload for {chapter}");
        ChapterDownloadPayload payload = mangaConnector.GetChapterPayload(mangaConnectorId);

        if (chapter.FullArchiveFilePath is not { } saveArchiveFilePath)
        {
            Log.Error("Failed getting saveArchiveFilePath");
            return [];
        }
        Log.Debug($"Chapter path: {saveArchiveFilePath}");
        
        //Check if Publication Directory already exists
        string? directoryPath = Path.GetDirectoryName(saveArchiveFilePath);
        if (directoryPath is null)
        {
            Log.Error($"Directory path could not be found: {saveArchiveFilePath}");
            this.Fail();
            return [];
        }
        if (!Directory.Exists(directoryPath))
        {
            Log.Info($"Creating publication Directory: {directoryPath}");
            Directory.CreateDirectory(directoryPath);
        }

        bool exported = await ChapterExporter.Export(payload, mangaConnector, chapter, saveArchiveFilePath,
            mangaConnectorId.WebsiteUrl, async () =>
            {
                await CopyCoverFromCacheToDownloadLocation(chapter.ParentManga);
                Log.Debug($"Loading collections {chapter}");
                foreach (CollectionEntry collectionEntry in MangaContext.Entry(chapter.ParentManga).Collections)
                    await collectionEntry.LoadAsync(CancellationToken);
            }, CancellationToken);
        if (!exported)
            return [];

        chapter.Downloaded = true;
        chapter.FileName = new FileInfo(saveArchiveFilePath).Name;
        if(await MangaContext.Sync(CancellationToken, GetType(), "Downloading complete") is { success: false } chapterContextException)
            Log.Error($"Failed to save database changes: {chapterContextException.exceptionMessage}");

        if (chapter.ParentManga.ContentKind == ContentKind.Novel)
        {
            Manga novel = await MangaContext.MangaWithMetadata().Include(manga => manga.Chapters)
                .SingleAsync(manga => manga.Key == chapter.ParentMangaId, CancellationToken);
            if (!await ChapterExporter.ExportNovelSeries(novel, novel.Chapters.Where(novelChapter => novelChapter.Downloaded), CancellationToken))
                Log.Warn($"Failed creating complete EPUB for {novel}");
            else if (ChapterExporter.RenameLegacyNovelSources(novel.Chapters) > 0 &&
                     await MangaContext.Sync(CancellationToken, GetType(), "Rename legacy novel chapter archives") is { success: false } renameException)
                Log.Error($"Failed to save renamed novel chapter archives: {renameException.exceptionMessage}");
        }
        
        Log.Debug($"Downloaded chapter {chapter}.");

        await ActionsContext.Actions.AddAsync(new ChapterDownloadedActionRecord(chapter.ParentManga, chapter));
        if(await ActionsContext.Sync(CancellationToken, GetType(), "Download complete") is { success: false } actionsContextException)
            Log.Error($"Failed to save database changes: {actionsContextException.exceptionMessage}");

        await NotificationsContext.Notifications.AddAsync(new Notification(
            "Chapter downloaded",
            $"{chapter.ParentManga.Name} Ch. {chapter.ChapterNumber} - {chapter.FileName}"
            ), CancellationToken);
        if(await NotificationsContext.Sync(CancellationToken, GetType(), "Download complete") is { success: false } notificationsContextException)
            Log.Error($"Failed to save database changes: {notificationsContextException.exceptionMessage}");

        bool refreshLibrary = await CheckLibraryRefresh();
        if(refreshLibrary)
            Log.Info($"Condition {Tranga.Settings.LibraryRefreshSetting} met.");

        return refreshLibrary? [new RefreshLibrariesWorker()] : [];
    }

    private async Task<bool> CheckLibraryRefresh() => Tranga.Settings.LibraryRefreshSetting switch
    {
        LibraryRefreshSetting.AfterAllFinished => await AllDownloadsFinished(),
        LibraryRefreshSetting.AfterMangaFinished => await MangaContext.MangaConnectorToChapter.Include(chId => chId.Obj).Where(chId => chId.UseForDownload).AllAsync(chId => chId.Obj.Downloaded, CancellationToken),
        LibraryRefreshSetting.AfterEveryChapter => true,
        LibraryRefreshSetting.WhileDownloading => await AllDownloadsFinished() ||  DateTime.UtcNow.Subtract(RefreshLibrariesWorker.LastRefresh).TotalMinutes > Tranga.Settings.RefreshLibraryWhileDownloadingEveryMinutes,
        _ => true
    };
    private async Task<bool> AllDownloadsFinished() => (await StartNewChapterDownloadsWorker.GetMissingChapters(MangaContext, CancellationToken)).Count == 0;
    
    private async Task CopyCoverFromCacheToDownloadLocation(Manga manga)
    {
        Log.Debug($"Copying cover for {manga}");

        manga = await MangaContext.MangaWithMetadata().Include(m => m.MangaConnectorIds).FirstAsync(m => m.Key == manga.Key, CancellationToken);
        string publicationFolder;
        try
        {
            Log.Debug("Checking Manga directory exists...");
            //Check if Publication already has a Folder and cover
            publicationFolder = manga.FullDirectoryPath;

            Log.Debug("Checking cover already exists...");
            DirectoryInfo dirInfo = new(publicationFolder);
            if (dirInfo.EnumerateFiles()
                .Any(info => info.Name.Contains("cover", StringComparison.InvariantCultureIgnoreCase)))
            {
                Log.Debug($"Cover already exists at {publicationFolder}");
                return;
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
            return;
        }

        if (manga.CoverFileNameInCache is not { } coverFileNameInCache)
        {
            MangaConnectorId<Manga> mangaConnectorId = manga.MangaConnectorIds.First();
            if (!Tranga.TryGetMangaConnector(mangaConnectorId.MangaConnectorName, out MangaConnector? mangaConnector))
            {
                Log.Error($"MangaConnector with name {mangaConnectorId.MangaConnectorName} could not be found");
                return;
            }
            
            coverFileNameInCache = mangaConnector.SaveCoverImageToCache(mangaConnectorId);
            manga.CoverFileNameInCache = coverFileNameInCache;
            if (await MangaContext.Sync(CancellationToken, reason: "Update cover filename") is { success: false } result)
                Log.Error($"Couldn't update cover filename {result.exceptionMessage}");
        }
        if (coverFileNameInCache is null)
        {
            Log.Error($"File {coverFileNameInCache} does not exist and failed to download cover");
            return;
        }
        
        string fullCoverPath = Path.Join(TrangaSettings.CoverImageCacheOriginal, coverFileNameInCache);
        string newFilePath = Path.Join(publicationFolder, $"cover.{Path.GetFileName(coverFileNameInCache).Split('.')[^1]}" );
        File.Copy(fullCoverPath, newFilePath, true);
        Log.Debug($"Copied cover from {fullCoverPath} to {newFilePath}");
    }

    public override string ToString() => $"{base.ToString()} {ChapterIdId}";
}
