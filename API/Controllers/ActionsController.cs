using API.Controllers.DTOs;
using API.Controllers.Requests;
using API.Schema.ActionsContext;
using API.Schema.ActionsContext.Actions;
using API.Schema.ActionsContext.Actions.Generic;
using API.Schema.MangaContext;
using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Microsoft.AspNetCore.Http.StatusCodes;
using ActionRecord = API.Controllers.DTOs.ActionRecord;

namespace API.Controllers;

[ApiVersion(2)]
[ApiController]
[Route("v{v:apiVersion}/[controller]")]
public class ActionsController(ActionsContext context, MangaContext mangaContext) : ControllerBase
{
    /// <summary>
    /// Returns the available Action Types (<see cref="Actions"/>)
    /// </summary>
    /// <response code="200">List of action-types</response>
    [HttpGet("Types")]
    [ProducesResponseType<Actions[]>(Status200OK, "application/json")]
    public Ok<Actions[]> GetAvailableActions()
    {
        return TypedResults.Ok(Enum.GetValues<Actions>());
    }

    /// <summary>
    /// Returns <see cref="Schema.ActionsContext.ActionRecord"/> performed in <see cref="Interval"/>
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="page">Page to request (default 1)</param>
    /// <param name="pageSize">Size of Page (default 10)</param>
    /// <response code="200">List of performed actions</response>
    /// <response code="400">Page data wrong</response>
    /// <response code="500">Database error</response>
    [HttpPost("Filter")]
    [ProducesResponseType<PagedResponse<ActionRecord>>(Status200OK, "application/json")]
    [ProducesResponseType(Status500InternalServerError)]
    [ProducesResponseType(Status400BadRequest)]
    public async Task<Results<Ok<PagedResponse<ActionRecord>>, BadRequest, InternalServerError>> GetActionsInterval([FromBody]ActionsFilterRecord filter, [FromQuery]int page = 1, [FromQuery]int pageSize = 10)
    {
        if (page < 1 || pageSize < 1)
            return TypedResults.BadRequest();
        if (await context.FilterActions(filter.MangaId, filter.ChapterId)
                .Where(a => filter.Start == null || a.PerformedAt >= filter.Start.Value.ToUniversalTime())
                .Where(a => filter.End == null || a.PerformedAt <= filter.End.Value.ToUniversalTime())
                .Where(a => filter.Action == null || a.Action == filter.Action)
                .CreatePagedResponse(a => a.PerformedAt, page, pageSize, HttpContext.RequestAborted)
            is not { } result)
            return TypedResults.InternalServerError();
        
        var actions = result.Data.ToList();
        string[] mangaIds = actions
            .OfType<IActionWithMangaRecord>()
            .Select(action => action.MangaId)
            .Distinct()
            .ToArray();
        string[] chapterIds = actions
            .OfType<IActionWithChapterRecord>()
            .Select(action => action.ChapterId)
            .Distinct()
            .ToArray();

        Dictionary<string, string> mangaTitles = mangaIds.Length == 0
            ? []
            : await mangaContext.Mangas
                .Where(manga => mangaIds.Contains(manga.Key))
                .ToDictionaryAsync(manga => manga.Key, manga => manga.Name, HttpContext.RequestAborted);

        var chapters = chapterIds.Length == 0
            ? []
            : await mangaContext.Chapters
                .Where(chapter => chapterIds.Contains(chapter.Key))
                .Select(chapter => new { chapter.Key, chapter.ChapterNumber, chapter.Title })
                .ToListAsync(HttpContext.RequestAborted);
        var chaptersById = chapters.ToDictionary(chapter => chapter.Key);

        return TypedResults.Ok(result.ToType(action =>
        {
            string? mangaId = action is IActionWithMangaRecord mangaAction ? mangaAction.MangaId : null;
            string? chapterId = action is IActionWithChapterRecord chapterAction ? chapterAction.ChapterId : null;
            chaptersById.TryGetValue(chapterId ?? string.Empty, out var chapter);
            mangaTitles.TryGetValue(mangaId ?? string.Empty, out string? mangaTitle);
            return new ActionRecord(action, mangaTitle, chapter?.ChapterNumber, chapter?.Title);
        }));
    }
}
