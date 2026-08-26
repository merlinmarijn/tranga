using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using API.Workers;

namespace API.Controllers.DTOs;

/// <summary>
/// <see cref="BaseWorker"/> DTO
/// </summary>
public record Worker(
    string Key,
    IEnumerable<string> Dependencies,
    IEnumerable<string> MissingDependencies,
    bool DependenciesFulfilled,
    WorkerExecutionState State,
    string? MangaId = null,
    string? MangaTitle = null,
    string? ChapterId = null,
    string? ChapterNumber = null,
    string? ChapterTitle = null) : Identifiable(Key)
{
    /// <summary>
    /// Workers this worker depends on having ran.
    /// </summary>
    [Required]
    [Description("Workers this worker depends on having ran.")]
    public IEnumerable<string> Dependencies { get; init; } = Dependencies;
    
    /// <summary>
    /// Workers that have not yet ran, that need to run for this Worker to run.
    /// </summary>
    [Required]
    [Description("Workers that have not yet ran, that need to run for this Worker to run.")]
    public IEnumerable<string> MissingDependencies { get; init; } = MissingDependencies;
    
    /// <summary>
    /// Worker can run.
    /// </summary>
    [Required]
    [Description("Worker can run.")]
    public bool DependenciesFulfilled { get; init; } = DependenciesFulfilled;
    
    /// <summary>
    /// Execution state of the Worker.
    /// </summary>
    [Required]
    [Description("Execution state of the Worker.")]
    public WorkerExecutionState State { get; init; } = State;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MangaId { get; init; } = MangaId;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MangaTitle { get; init; } = MangaTitle;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChapterId { get; init; } = ChapterId;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChapterNumber { get; init; } = ChapterNumber;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChapterTitle { get; init; } = ChapterTitle;
}
