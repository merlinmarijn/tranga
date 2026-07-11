using API.MangaConnectors;
using HtmlAgilityPack;

namespace Tests;

public class LightNovelWorldChapterTitleTest
{
    [Theory]
    [InlineData("Chapter 1 - Crimson", "1", "Crimson")]
    [InlineData("Chapter 51 Change of plans", "51", "Change of plans")]
    [InlineData("Chapter 1648: New Deity title", "1648", "New Deity title")]
    public void ChapterTitles_AcceptObservedSeparators(string text, string number, string title)
    {
        HtmlDocument document = new();
        document.LoadHtml($"<div><div class='chapter-number'>{number}</div><h3>{text}</h3></div>");
        (string Number, string Title)? chapter = LightNovelWorld.ParseChapter(document.DocumentNode.SelectSingleNode("//div")!);

        Assert.NotNull(chapter);
        Assert.Equal(number, chapter.Value.Number);
        Assert.Equal(title, chapter.Value.Title);
    }

    [Fact]
    public void ChapterTitles_RemoveTheSiteBomBeforeParsing()
    {
        HtmlDocument document = new();
        document.LoadHtml("<div><div class='chapter-number'>252</div><h3>Chapter \uFEFF252 Punishment</h3></div>");
        (string Number, string Title)? chapter = LightNovelWorld.ParseChapter(document.DocumentNode.SelectSingleNode("//div")!);

        Assert.Equal(("252", "Punishment"), chapter);
    }
}
