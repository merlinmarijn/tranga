using API.Controllers.DTOs;
using API.Schema.MangaContext;
using API.Workers;
using API.Workers.MangaDownloadWorkers;
using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Microsoft.AspNetCore.Http.StatusCodes;
// ReSharper disable InconsistentNaming

namespace API.Controllers;

[ApiVersion(2)]
[ApiController]
[Route("v{version:apiVersion}/[controller]")]
public class WorkerController(MangaContext context) : ControllerBase
{
    /// <summary>
    /// Returns all <see cref="BaseWorker"/>
    /// </summary>
    /// <response code="200"><see cref="Worker"/></response>
    [HttpGet]
    [ProducesResponseType<List<Worker>>(Status200OK, "application/json")]
    public async Task<Ok<List<Worker>>> GetWorkers()
    {
        BaseWorker[] workers = Tranga.GetKnownWorkers();
        IReadOnlyDictionary<string, DownloadDetails> downloadDetails = await GetDownloadDetails(workers);
        return TypedResults.Ok(workers.Select(worker => ToDto(worker, downloadDetails)).ToList());
    }

    /// <summary>
    /// Get all <see cref="BaseWorker"/> in requested <see cref="WorkerExecutionState"/>
    /// </summary>
    /// <param name="State">Requested <see cref="WorkerExecutionState"/></param>
    /// <response code="200"></response>
    [HttpGet("State/{State}")]
    [ProducesResponseType<List<Worker>>(Status200OK, "application/json")]
    public async Task<Ok<List<Worker>>> GetWorkersInState(WorkerExecutionState State)
    {
        BaseWorker[] workers = Tranga.GetKnownWorkers().Where(worker => worker.State == State).ToArray();
        IReadOnlyDictionary<string, DownloadDetails> downloadDetails = await GetDownloadDetails(workers);
        return TypedResults.Ok(workers.Select(worker => ToDto(worker, downloadDetails)).ToList());
    }

    /// <summary>
    /// Return <see cref="BaseWorker"/> with <paramref name="WorkerId"/>
    /// </summary>
    /// <param name="WorkerId"><see cref="BaseWorker"/>.Key</param>
    /// <response code="200"></response>
    /// <response code="404"><see cref="BaseWorker"/> with <paramref name="WorkerId"/> could not be found</response>
    [HttpGet("{WorkerId}")]
    [ProducesResponseType<Worker>(Status200OK, "application/json")]
    [ProducesResponseType<string>(Status404NotFound, "text/plain")]
    public async Task<Results<Ok<Worker>, NotFound<string>>> GetWorker(string WorkerId)
    {
        if(Tranga.GetKnownWorkers().FirstOrDefault(w => w.Key == WorkerId) is not { } w)
            return TypedResults.NotFound(nameof(WorkerId));
        
        IReadOnlyDictionary<string, DownloadDetails> downloadDetails = await GetDownloadDetails([w]);
        return TypedResults.Ok(ToDto(w, downloadDetails));
    }

    /// <summary>
    /// Stops <see cref="BaseWorker"/> with <paramref name="WorkerId"/>
    /// </summary>
    /// <param name="WorkerId"><see cref="BaseWorker"/>.Key</param>
    /// <response code="200"></response>
    /// <response code="404"><see cref="BaseWorker"/> with <paramref name="WorkerId"/> could not be found</response>
    /// <response code="412"><see cref="BaseWorker"/> was already not running</response>
    [HttpPost("{WorkerId}/Stop")]
    [ProducesResponseType(Status202Accepted)]
    [ProducesResponseType<string>(Status404NotFound, "text/plain")]
    [ProducesResponseType(Status412PreconditionFailed)]
    public Results<Ok, NotFound<string>, StatusCodeHttpResult> StopWorker(string WorkerId)
    {
        if(Tranga.GetRunningWorkers().FirstOrDefault(w => w.Key == WorkerId) is not { } worker)
            return TypedResults.NotFound(nameof(WorkerId));
        
        if(worker.State is < WorkerExecutionState.Running or >= WorkerExecutionState.Completed)
            return TypedResults.StatusCode(Status412PreconditionFailed);
        
        Tranga.StopWorker(worker);
        return TypedResults.Ok();
    }

    private async Task<IReadOnlyDictionary<string, DownloadDetails>> GetDownloadDetails(IEnumerable<BaseWorker> workers)
    {
        string[] connectorIds = workers
            .OfType<DownloadChapterFromMangaconnectorWorker>()
            .Select(worker => worker.ChapterIdId)
            .Distinct()
            .ToArray();

        if (connectorIds.Length == 0)
            return new Dictionary<string, DownloadDetails>();

        var details = await context.MangaConnectorToChapter
            .Where(connectorId => connectorIds.Contains(connectorId.Key))
            .Select(connectorId => new
            {
                ConnectorId = connectorId.Key,
                ChapterId = connectorId.ObjId,
                MangaId = connectorId.Obj.ParentMangaId,
                MangaTitle = connectorId.Obj.ParentManga.Name,
                connectorId.Obj.ChapterNumber,
                connectorId.Obj.Title
            })
            .ToListAsync(HttpContext.RequestAborted);

        return details.ToDictionary(
            detail => detail.ConnectorId,
            detail => new DownloadDetails(
                detail.MangaId,
                detail.MangaTitle,
                detail.ChapterId,
                detail.ChapterNumber,
                detail.Title));
    }

    private static Worker ToDto(BaseWorker worker, IReadOnlyDictionary<string, DownloadDetails> downloadDetails)
    {
        DownloadDetails? details = worker is DownloadChapterFromMangaconnectorWorker downloadWorker
                                   && downloadDetails.TryGetValue(downloadWorker.ChapterIdId, out DownloadDetails? found)
            ? found
            : null;

        return new Worker(
            worker.Key,
            worker.AllDependencies.Select(dependency => dependency.Key),
            worker.MissingDependencies.Select(dependency => dependency.Key),
            worker.AllDependenciesFulfilled,
            worker.State,
            details?.MangaId,
            details?.MangaTitle,
            details?.ChapterId,
            details?.ChapterNumber,
            details?.ChapterTitle);
    }

    private sealed record DownloadDetails(
        string MangaId,
        string MangaTitle,
        string ChapterId,
        string ChapterNumber,
        string? ChapterTitle);
}
