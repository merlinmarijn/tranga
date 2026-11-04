using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers.DTOs;

/// <summary>
/// Compact <see cref="API.Schema.MangaContext.Chapter"/> for bulk library views
/// </summary>
public sealed record ChapterSummary(
    [property: Required, Description("Chapter key")] string Key,
    [property: Required, Description("Key of the Manga this Chapter belongs to")] string MangaId,
    [property: Description("Volume number")] int? Volume,
    [property: Required, Description("Chapter number")] string ChapterNumber,
    [property: Description("Chapter title")] string? Title,
    [property: Required, Description("Whether the archive exists")] bool Downloaded);
