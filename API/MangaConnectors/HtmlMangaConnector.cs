using System.Text.RegularExpressions;
using System.Web;
using API.MangaDownloadClients;
using API.Schema.MangaContext;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace API.MangaConnectors;

/// <summary>
/// Declarative connector for sites whose search, series, chapter, and reader pages are regular HTML.
/// Use <see cref="MangaConnector"/> directly for APIs, JavaScript-only pages, or unusual workflows.
/// </summary>
public abstract class HtmlMangaConnector : MangaConnector
{
    protected HtmlConnectorDefinition Definition { get; }

    protected HtmlMangaConnector(HtmlConnectorDefinition definition, IDownloadClient client) : base(
        definition.Name,
        definition.SupportedLanguages,
        definition.BaseUris,
        definition.IconUrl)
    {
        Definition = definition.Validate();
        downloadClient = client;
    }

    protected HtmlMangaConnector(HtmlConnectorDefinition definition) : this(definition, new HttpDownloadClient())
    {
    }

    public override (Manga, MangaConnectorId<Manga>)[] SearchManga(string mangaSearchName)
    {
        string query = HttpUtility.UrlEncode(mangaSearchName).Replace("+", Definition.SearchQuerySpaceReplacement ?? "+");
        HtmlDocument? document = GetDocument(BuildUrl(Definition.SearchUrl, query), RequestType.Default);
        if (Definition.NextData && document is not null)
            return NextData(document, "ssrItems") is not JArray items
                ? []
                : items.Select(CreateManga).ToArray();

        HtmlNodeCollection? links = document?.DocumentNode.SelectNodes(Definition.SearchResultXPath);
        if (links is null)
            return [];

        return links
            .Select(link => link.GetAttributeValue("href", ""))
            .Where(href => !string.IsNullOrWhiteSpace(href))
            .Select(ToAbsoluteUrl)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(GetMangaFromUrl)
            .Where(result => result.HasValue)
            .Select(result => result!.Value)
            .DistinctBy(result => result.Item2.IdOnConnectorSite)
            .ToArray();
    }

    public override (Manga, MangaConnectorId<Manga>)? GetMangaFromUrl(string url)
    {
        Match match = new Regex(Definition.MangaIdRegex, RegexOptions.IgnoreCase).Match(url);
        return !match.Success ? null : GetMangaFromId(match.Groups["id"].Value);
    }

    public override (Manga, MangaConnectorId<Manga>)? GetMangaFromId(string mangaIdOnSite)
    {
        string url = BuildUrl(Definition.MangaUrl, mangaIdOnSite);
        HtmlDocument? document = GetDocument(url, RequestType.MangaInfo);
        if (Definition.NextData && document is not null)
            return NextData(document, "initialManga") is not JToken manga ? null : CreateManga(manga);

        return document is null ? null : CreateManga(document, mangaIdOnSite, url);
    }

    public override (Chapter, MangaConnectorId<Chapter>)[] GetChapters(MangaConnectorId<Manga> mangaId, string? language = null)
    {
        HtmlDocument? document = GetDocument(mangaId.WebsiteUrl ?? BuildUrl(Definition.MangaUrl, mangaId.IdOnConnectorSite), RequestType.Default);
        if (Definition.NextData && document is not null)
            return NextData(document, "initialManga")?["chapters"] is not JArray chapters
                ? []
                : chapters.Select(chapter => CreateChapter(mangaId.Obj, ToAbsoluteUrl(chapter.Value<string>("url") ?? string.Empty), chapter.Value<string>("name") ?? string.Empty))
                    .Where(result => result.HasValue)
                    .Select(result => result!.Value)
                    .OrderBy(result => result.Item1, new Chapter.ChapterComparer())
                    .ToArray();

        HtmlNodeCollection? links = document?.DocumentNode.SelectNodes(Definition.ChapterLinkXPath);
        if (links is null)
            return [];

        return links
            .Select(link => CreateChapter(mangaId.Obj, link))
            .Where(result => result.HasValue)
            .Select(result => result!.Value)
            .DistinctBy(result => result.Item2.IdOnConnectorSite)
            .OrderBy(result => result.Item1, new Chapter.ChapterComparer())
            .ToArray();
    }

    internal override string[] GetChapterImageUrls(MangaConnectorId<Chapter> chapterId)
    {
        HtmlDocument? document = chapterId.WebsiteUrl is null ? null : GetDocument(chapterId.WebsiteUrl, RequestType.Default);
        if (document is null)
            return [];

        if (Definition.NextData && NextData(document, "initialChapter")?["images"] is JArray imageUrls)
            return imageUrls.Values<string>().Where(url => !string.IsNullOrWhiteSpace(url)).ToArray()!;

        if (Definition.PageImageRegex is { } pageImageRegex)
            return Regex.Matches(document.DocumentNode.OuterHtml, pageImageRegex)
                .SelectMany(match => match.Groups["url"].Captures.Select(capture => capture.Value))
                .Distinct()
                .ToArray();

        HtmlNodeCollection? images = document?.DocumentNode.SelectNodes(Definition.PageImageXPath);
        return images?.Select(image => image.GetAttributeValue(Definition.PageImageAttribute, ""))
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(ToAbsoluteUrl)
            .ToArray() ?? [];
    }

    private (Manga, MangaConnectorId<Manga>) CreateManga(HtmlDocument document, string id, string url)
    {
        string title = Text(document.DocumentNode, Definition.Title) ?? id;
        string coverUrl = Text(document.DocumentNode, Definition.Cover) ?? string.Empty;
        Manga manga = new(
            title,
            Text(document.DocumentNode, Definition.Description) ?? string.Empty,
            string.IsNullOrWhiteSpace(coverUrl) ? coverUrl : ToAbsoluteUrl(coverUrl),
            Status(Text(document.DocumentNode, Definition.Status)),
            Texts(document.DocumentNode, Definition.Authors).Select(author => new Author(author)).ToList(),
            Texts(document.DocumentNode, Definition.Tags).Select(tag => new MangaTag(tag)).ToList(),
            [],
            [],
            originalLanguage: Definition.OriginalLanguage);
        MangaConnectorId<Manga> connectorId = new(manga, this, id, url);
        manga.MangaConnectorIds.Add(connectorId);
        return (manga, connectorId);
    }

    private (Manga, MangaConnectorId<Manga>) CreateManga(JToken token)
    {
        string id = token.Value<string>("slug") ?? throw new InvalidOperationException("MangaK data did not include a slug.");
        string url = ToAbsoluteUrl(token.Value<string>("url") ?? id);
        string title = token.Value<string>("name") ?? id;
        string coverUrl = token.Value<string>("cover") ?? string.Empty;
        JToken tagsToken = token["tags"] is { HasValues: true } definedTags ? definedTags : token["genres"] ?? new JArray();
        Manga manga = new(
            title,
            token.Value<string>("summary") ?? string.Empty,
            coverUrl,
            Status(token.Value<string>("status")),
            (token["authors"]?.Children() ?? Enumerable.Empty<JToken>()).Select(author => new Author(author.Value<string>("name") ?? author.Value<string>() ?? string.Empty)).Where(author => author.AuthorName.Length > 0).ToList(),
            tagsToken.Children().Select(tag => new MangaTag(tag.Value<string>("name") ?? tag.Value<string>() ?? string.Empty)).Where(tag => tag.Tag.Length > 0).ToList(),
            [],
            [],
            originalLanguage: Definition.OriginalLanguage);
        MangaConnectorId<Manga> connectorId = new(manga, this, id, url);
        manga.MangaConnectorIds.Add(connectorId);
        return (manga, connectorId);
    }

    private (Chapter, MangaConnectorId<Chapter>)? CreateChapter(Manga manga, HtmlNode link)
    {
        string url = ToAbsoluteUrl(link.GetAttributeValue("href", ""));
        return CreateChapter(manga, url, HtmlEntity.DeEntitize(link.InnerText));
    }

    private (Chapter, MangaConnectorId<Chapter>)? CreateChapter(Manga manga, string url, string text)
    {
        Match match = new Regex(Definition.ChapterRegex, RegexOptions.IgnoreCase).Match(url + " " + text);
        if (!match.Success || !match.Groups["number"].Success)
            return null;

        int? volume = int.TryParse(match.Groups["volume"].Value, out int parsedVolume) ? parsedVolume : null;
        string title = match.Groups["title"].Success ? match.Groups["title"].Value.Trim() : string.Empty;
        Chapter chapter = new(manga, match.Groups["number"].Value, volume, string.IsNullOrWhiteSpace(title) ? null : title);
        MangaConnectorId<Chapter> connectorId = new(chapter, this, url, url);
        chapter.MangaConnectorIds.Add(connectorId);
        return (chapter, connectorId);
    }

    private static JToken? NextData(HtmlDocument document, string key)
    {
        string? json = document.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']")?.InnerText;
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JObject.Parse(json)["props"]?["pageProps"]?[key];
        }
        catch (Exception)
        {
            return null;
        }
    }

    private HtmlDocument? GetDocument(string url, RequestType requestType)
    {
        using HttpResponseMessage response = downloadClient.MakeRequest(url, requestType).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            Log.WarnFormat("{0} returned {1}", url, response.StatusCode);
            return null;
        }

        HtmlDocument document = new();
        document.LoadHtml(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return document;
    }

    private MangaReleaseStatus Status(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "ongoing" => MangaReleaseStatus.Continuing,
        "completed" => MangaReleaseStatus.Completed,
        "hiatus" => MangaReleaseStatus.OnHiatus,
        "cancelled" or "canceled" or "dropped" => MangaReleaseStatus.Cancelled,
        _ => MangaReleaseStatus.Unreleased
    };

    private static string? Text(HtmlNode node, HtmlValueSelector? selector) => selector is null ? null : selector.Read(node);

    private static IEnumerable<string> Texts(HtmlNode node, string? xpath) => xpath is null
        ? []
        : (node.SelectNodes(xpath) ?? Enumerable.Empty<HtmlNode>()).Select(item => HtmlEntity.DeEntitize(item.InnerText).Trim()).Where(value => value.Length > 0);

    private string ToAbsoluteUrl(string url) => new Uri(new Uri(Definition.BaseUrl), url).ToString();

    private static string BuildUrl(string template, string value) => template.Replace("{id}", value).Replace("{query}", value);
}

public sealed record HtmlConnectorDefinition(
    string Name,
    string BaseUrl,
    string[] SupportedLanguages,
    string[] BaseUris,
    string IconUrl,
    string SearchUrl,
    string SearchResultXPath,
    string MangaUrl,
    string MangaIdRegex,
    HtmlValueSelector Title,
    string ChapterLinkXPath,
    string ChapterRegex,
    string PageImageXPath)
{
    public string PageImageAttribute { get; init; } = "src";
    public string? PageImageRegex { get; init; }
    public bool NextData { get; init; }
    public HtmlValueSelector? Description { get; init; }
    public HtmlValueSelector? Cover { get; init; }
    public HtmlValueSelector? Status { get; init; }
    public string? Authors { get; init; }
    public string? Tags { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? SearchQuerySpaceReplacement { get; init; }

    public HtmlConnectorDefinition Validate()
    {
        string[] required = [Name, BaseUrl, IconUrl, SearchUrl, SearchResultXPath, MangaUrl, MangaIdRegex, ChapterLinkXPath, ChapterRegex, PageImageXPath];
        if (required.Any(string.IsNullOrWhiteSpace) || SupportedLanguages.Length == 0 || BaseUris.Length == 0)
            throw new ArgumentException("Connector definitions need names, URLs, XPaths, and at least one language and domain.");
        if (!MangaIdRegex.Contains("(?<id>", StringComparison.Ordinal) || !ChapterRegex.Contains("(?<number>", StringComparison.Ordinal))
            throw new ArgumentException("MangaIdRegex needs a named 'id' group and ChapterRegex needs a named 'number' group.");
        if (PageImageRegex is not null && !PageImageRegex.Contains("(?<url>", StringComparison.Ordinal))
            throw new ArgumentException("PageImageRegex needs a named 'url' group.");

        _ = new Uri(BaseUrl, UriKind.Absolute);
        _ = new Regex(MangaIdRegex);
        _ = new Regex(ChapterRegex);
        if (PageImageRegex is not null)
            _ = new Regex(PageImageRegex);
        return this;
    }
}

public sealed record HtmlValueSelector(string XPath, string? Attribute = null)
{
    public string? Read(HtmlNode node)
    {
        HtmlNode? value = node.SelectSingleNode(XPath);
        string text = Attribute is null ? value?.InnerText ?? string.Empty : value?.GetAttributeValue(Attribute, string.Empty) ?? string.Empty;
        text = HtmlEntity.DeEntitize(text).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
