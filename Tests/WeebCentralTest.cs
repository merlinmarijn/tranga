using System.Net;
using API.MangaConnectors;
using API.MangaDownloadClients;

namespace Tests;

public class WeebCentralTest
{
    [Fact]
    public void CurrentMarkup_SupportsSearchMetadataChaptersAndImages()
    {
        StubDownloadClient client = new();
        WeebCentral connector = new() { downloadClient = client };

        var searchResult = Assert.Single(connector.SearchManga("The Dark Mage's Return to Enlistment"));

        Assert.Equal("The Dark Mage's Return to Enlistment", searchResult.Item1.Name);
        Assert.Equal(["Golden Dove", "Haeil", "O'Comic"], searchResult.Item1.Authors.Select(author => author.AuthorName));
        Assert.Equal(["Action", "Comedy"], searchResult.Item1.MangaTags.Select(tag => tag.Tag));
        Assert.Equal((uint)2023, searchResult.Item1.Year);

        var chapters = connector.GetChapters(searchResult.Item2);

        Assert.Equal(["1", "2"], chapters.Select(chapter => chapter.Item1.ChapterNumber));
        Assert.Equal("https://weebcentral.com/chapters/chapter-2", chapters[1].Item2.WebsiteUrl);

        string[] imageUrls = connector.GetChapterImageUrls(chapters[1].Item2);

        Assert.Equal([
            "https://images.example.test/002-001.webp",
            "https://images.example.test/002-002.webp"
        ], imageUrls);
        Assert.Contains(client.RequestedUrls,
            url => url.EndsWith("/chapters/chapter-2/images?is_prev=False&reading_style=long_strip&current_page=1"));
    }

    private sealed class StubDownloadClient : IDownloadClient
    {
        public List<string> RequestedUrls { get; } = [];

        public Task<HttpResponseMessage> MakeRequest(string url, RequestType requestType, string? referrer = null,
            CancellationToken? cancellationToken = null)
        {
            RequestedUrls.Add(url);
            string? response = url switch
            {
                _ when url.Contains("/search/data?") => """
                    <a href="/series/series-id/the-dark-mages-return-to-enlistment">The Dark Mage's Return to Enlistment</a>
                    """,
                "https://weebcentral.com/series/series-id/the-dark-mages-return-to-enlistment" => """
                    <html><head><title>The Dark Mage&#39;s Return to Enlistment | Weeb Central</title></head><body>
                    <img src="https://images.example.test/cover.webp" alt="The Dark Mage's Return to Enlistment cover">
                    <ul>
                      <li><strong>Author(s): </strong><span><a>Golden Dove</a>,</span><span><a>Haeil</a>,</span><span><a>O&#39;Comic</a></span></li>
                      <li><strong>Tags(s): </strong><span><a>Action</a>,</span><span><a>Comedy</a></span></li>
                      <li><strong>Status: </strong><a>Ongoing</a></li>
                      <li><strong>Released: </strong><span>2023</span></li>
                      <li><strong>Description</strong><p>A returning mage has to enlist.</p></li>
                    </ul>
                    </body></html>
                    """,
                "https://weebcentral.com/series/series-id/full-chapter-list" => """
                    <a href="/chapters/chapter-2"><span class="">Chapter 2</span></a>
                    <a href="/chapters/chapter-1"><span class="">Chapter 1</span></a>
                    """,
                "https://weebcentral.com/chapters/chapter-2/images?is_prev=False&reading_style=long_strip&current_page=1" => """
                    <img src="https://images.example.test/002-001.webp" alt="Page 1">
                    <img data-src="https://images.example.test/002-002.webp" alt="Page 2">
                    """,
                _ => null
            };

            return Task.FromResult(new HttpResponseMessage(response is null ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            {
                Content = new StringContent(response ?? string.Empty)
            });
        }
    }
}
