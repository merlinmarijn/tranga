using API.MangaConnectors;
using HtmlAgilityPack;

namespace Tests;

public class LightNovelWorldTest
{
    [Fact]
    public void ChapterPageCount_UsesTheIndexSelector()
    {
        HtmlDocument document = new();
        document.LoadHtml("<main><select><option value='1'>1</option><option value='29'>29</option></select></main>");

        Assert.Equal(29, LightNovelWorld.GetChapterPageCount(document));
    }

    [Fact]
    public void ChapterIds_AreUniqueAcrossNovels()
    {
        Assert.NotEqual(LightNovelWorld.CreateChapterId("lord-of-the-mysteries", "1"),
            LightNovelWorld.CreateChapterId("the-innkeeper", "1"));
    }
}
