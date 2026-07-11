using System.Text.RegularExpressions;

namespace Tests;

public class LightNovelWorldChapterTitleTest
{
    [Theory]
    [InlineData("Chapter 1 - Crimson", "1", "Crimson")]
    [InlineData("Chapter 51 Change of plans", "51", "Change of plans")]
    [InlineData("Chapter 1648: New Deity title", "1648", "New Deity title")]
    public void ChapterTitles_AcceptObservedSeparators(string text, string number, string title)
    {
        Match match = Regex.Match(text, @"Chapter\s+(?<number>[\d.]+)(?:\s*[-:]\s*|\s+)(?<title>.*)");

        Assert.Equal(number, match.Groups["number"].Value);
        Assert.Equal(title, match.Groups["title"].Value);
    }
}
