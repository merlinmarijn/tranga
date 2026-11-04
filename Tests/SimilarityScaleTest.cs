using Soenneker.Utils.String.NeedlemanWunsch;

namespace Tests;

public class SimilarityScaleTest
{
    // MangaController.WithSimilarName and Chapter.CheckDownloaded threshold against a 0-100 scale
    [Fact]
    public void CalculateSimilarityPercentage_UsesPercentScale()
    {
        Assert.Equal(100, NeedlemanWunschStringUtil.CalculateSimilarityPercentage("One Piece", "One Piece"), 3);
        Assert.True(NeedlemanWunschStringUtil.CalculateSimilarityPercentage("One Piece", "Naruto") < 80);
    }
}
