using System.ComponentModel.DataAnnotations;
using API.Schema.ActionsContext.Actions.Generic;
using API.Schema.MangaContext;

namespace API.Schema.ActionsContext.Actions;

public sealed class ChapterDownloadFailedActionRecord(Actions action, DateTime performedAt, string mangaId, string chapterId, string reason) : ActionRecord(action, performedAt), IActionWithChapterRecord, IActionWithMangaRecord
{
    public ChapterDownloadFailedActionRecord(Manga manga, Chapter chapter, string reason) : this(Actions.ChapterDownloadFailed, DateTime.UtcNow, manga.Key, chapter.Key, reason) { }
    public string ChapterId { get; init; } = chapterId;
    public string MangaId { get; init; } = mangaId;

    /// <summary>
    /// Single-line reason why the download failed
    /// </summary>
    [StringLength(2048)]
    public string Reason { get; init; } = reason;
}
