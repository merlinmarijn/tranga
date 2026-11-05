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
    string? ChapterTitle = null,
    string Type = "",
    string Description = "",
    string? StatusMessage = null,
    int? ProgressCurrent = null,
    int? ProgressTotal = null,
    DateTime? StartedAt = null,
    DateTime? FinishedAt = null,
    bool IsPeriodic = false,
    TimeSpan? Interval = null,
    DateTime? LastExecution = null,
    DateTime? NextExecution = null) : Identifiable(Key)
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

    /// <summary>
    /// Worker class name without the "Worker" suffix.
    /// </summary>
    [Required]
    [Description("Worker class name without the \"Worker\" suffix.")]
    public string Type { get; init; } = Type;

    /// <summary>
    /// Human-readable description of what the Worker does.
    /// </summary>
    [Required]
    [Description("Human-readable description of what the Worker does.")]
    public string Description { get; init; } = Description;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Current step of the Worker.")]
    public string? StatusMessage { get; init; } = StatusMessage;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProgressCurrent { get; init; } = ProgressCurrent;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProgressTotal { get; init; } = ProgressTotal;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? StartedAt { get; init; } = StartedAt;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? FinishedAt { get; init; } = FinishedAt;

    /// <summary>
    /// Worker runs repeatedly at Interval.
    /// </summary>
    [Required]
    [Description("Worker runs repeatedly at Interval.")]
    public bool IsPeriodic { get; init; } = IsPeriodic;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Interval at which Worker runs.")]
    public TimeSpan? Interval { get; init; } = Interval;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Timestamp when Worker executed last.")]
    public DateTime? LastExecution { get; init; } = LastExecution;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [Description("Timestamp when Worker is scheduled to execute next.")]
    public DateTime? NextExecution { get; init; } = NextExecution;
}
