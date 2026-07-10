using System.Text.RegularExpressions;
using System.Web;
using API.MangaDownloadClients;
using API.Schema.MangaContext;
using HtmlAgilityPack;

namespace API.MangaConnectors;

public sealed class LightNovelWorld : MangaConnector
{
    public LightNovelWorld() : base("LightNovelWorld", ["en"], ["lightnovelworld.org"], "https://lightnovelworld.org/favicon.ico", ContentKind.Novel)
    {
        downloadClient = new HttpDownloadClient();
    }

    public override (Manga, MangaConnectorId<Manga>)[] SearchManga(string mangaSearchName)
    {
        ChromiumDownloadClient chromium = new();
        try
        {
            HtmlDocument? document = GetDocument($"https://lightnovelworld.org/search/?q={HttpUtility.UrlEncode(mangaSearchName)}", chromium);
            return (document?.DocumentNode.SelectNodes("//main//a[contains(@href, '/novel/')][.//h3]") ?? Enumerable.Empty<HtmlNode>())
                .Select(link => GetMangaFromUrl(ToUrl(link.GetAttributeValue("href", ""))))
                .Where(result => result.HasValue).Select(result => result!.Value).DistinctBy(result => result.Item2.IdOnConnectorSite).ToArray();
        }
        finally
        {
            chromium.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public override (Manga, MangaConnectorId<Manga>)? GetMangaFromUrl(string url)
    {
        Match match = Regex.Match(url, @"/novel/(?<id>[^/?#]+)/?$");
        return match.Success ? GetMangaFromId(match.Groups["id"].Value) : null;
    }

    public override (Manga, MangaConnectorId<Manga>)? GetMangaFromId(string mangaIdOnSite)
    {
        string url = $"https://lightnovelworld.org/novel/{mangaIdOnSite}/";
        HtmlDocument? document = GetDocument(url);
        HtmlNode? main = document?.DocumentNode.SelectSingleNode("//main");
        if (main is null)
            return null;

        string name = Text(main, ".//h1") ?? mangaIdOnSite;
        string description = Text(main, ".//h2[normalize-space()='Summary']/following-sibling::p[1]") ?? string.Empty;
        string cover = main.SelectSingleNode(".//img[@alt]")?.GetAttributeValue("src", "") ?? string.Empty;
        string author = Text(main, ".//p[contains(normalize-space(), 'Author:')]//a") ?? string.Empty;
        Manga manga = new(name, description, ToUrl(cover), Status(main.InnerText), string.IsNullOrEmpty(author) ? [] : [new Author(author)],
            (main.SelectNodes(".//a[contains(@href, 'tags_include')]") ?? Enumerable.Empty<HtmlNode>()).Select(node => new MangaTag(node.InnerText.Trim())).ToList(), [], [], originalLanguage: "en", contentKind: ContentKind.Novel);
        MangaConnectorId<Manga> id = new(manga, this, mangaIdOnSite, url);
        manga.MangaConnectorIds.Add(id);
        return (manga, id);
    }

    public override (Chapter, MangaConnectorId<Chapter>)[] GetChapters(MangaConnectorId<Manga> mangaId, string? language = null)
    {
        List<(Chapter, MangaConnectorId<Chapter>)> chapters = [];
        for (int page = 1; ; page++)
        {
            HtmlDocument? document = GetDocument($"https://lightnovelworld.org/novel/{mangaId.IdOnConnectorSite}/chapters/?page={page}");
            HtmlNodeCollection? cards = document?.DocumentNode.SelectNodes("//main//div[contains(@class, 'chapter-card')]");
            if (cards is null || cards.Count == 0)
                break;
            foreach (HtmlNode card in cards)
            {
                Match match = Regex.Match(card.GetAttributeValue("onclick", ""), @"chapter/(?<id>\d+)/");
                Match title = Regex.Match(Text(card, ".//h3") ?? "", @"Chapter\s+(?<number>[\d.]+)\s*-\s*(?<title>.*)");
                if (!match.Success || !title.Success)
                    continue;
                Chapter chapter = new(mangaId.Obj, title.Groups["number"].Value, null, title.Groups["title"].Value.Trim());
                MangaConnectorId<Chapter> id = new(chapter, this, match.Groups["id"].Value,
                    $"https://lightnovelworld.org/novel/{mangaId.IdOnConnectorSite}/chapter/{match.Groups["id"].Value}/");
                chapter.MangaConnectorIds.Add(id);
                chapters.Add((chapter, id));
            }
        }
        return chapters.OrderBy(item => item.Item1, new Chapter.ChapterComparer()).ToArray();
    }

    internal override ChapterDownloadPayload GetChapterPayload(MangaConnectorId<Chapter> chapterId) =>
        GetDocument(chapterId.WebsiteUrl ?? string.Empty)?.DocumentNode.SelectSingleNode("//main//div[contains(@class, 'chapter-text')]") is { } content
            ? new ChapterHtmlPayload(content.InnerHtml)
            : new ChapterHtmlPayload(string.Empty);

    private HtmlDocument? GetDocument(string url, IDownloadClient? client = null)
    {
        using HttpResponseMessage response = (client ?? downloadClient).MakeRequest(url, RequestType.Default).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            return null;
        HtmlDocument document = new();
        document.LoadHtml(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return document;
    }

    private static string? Text(HtmlNode node, string xpath) => node.SelectSingleNode(xpath) is { } value
        ? HtmlEntity.DeEntitize(value.InnerText).Trim() : null;
    private static string ToUrl(string url) => string.IsNullOrWhiteSpace(url) ? string.Empty : new Uri(new Uri("https://lightnovelworld.org"), url).ToString();
    private static MangaReleaseStatus Status(string text) => text.Contains("Completed", StringComparison.OrdinalIgnoreCase) ? MangaReleaseStatus.Completed : MangaReleaseStatus.Continuing;
}
