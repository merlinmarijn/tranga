namespace API.MangaConnectors;

public abstract record ChapterDownloadPayload;

public sealed record ImagePagesPayload(string[] Urls) : ChapterDownloadPayload;

public sealed record ChapterHtmlPayload(string Html) : ChapterDownloadPayload;
