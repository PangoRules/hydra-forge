using HydraForge.Tui.Renderers;

namespace HydraForge.Tui.Tests;

public class ColumnScrollCalculatorTests
{
    [Fact]
    public void CardBoxHeight_NoBadges_IsBorderPlusTitle()
    {
        Assert.Equal(3, ColumnScrollCalculator.CardBoxHeight(0));
    }

    [Fact]
    public void CardBoxHeight_UnderCap_OneLinePerBadge()
    {
        Assert.Equal(6, ColumnScrollCalculator.CardBoxHeight(3));
    }

    [Fact]
    public void CardBoxHeight_OverCap_CapsAtFiveAndAddsOverflowLine()
    {
        // 2 border + 1 title + 5 shown + 1 overflow line
        Assert.Equal(9, ColumnScrollCalculator.CardBoxHeight(8));
    }

    [Fact]
    public void ComputeVisibleRange_EmptyColumn_ReturnsEmptyWindow()
    {
        var result = ColumnScrollCalculator.ComputeVisibleRange([], null, 20);

        Assert.Equal(0, result.Start);
        Assert.Equal(0, result.End);
        Assert.False(result.HasMoreAbove);
        Assert.False(result.HasMoreBelow);
    }

    [Fact]
    public void ComputeVisibleRange_AllCardsFit_NoIndicators()
    {
        var heights = new[] { 3, 3, 3 };

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, null, 20);

        Assert.Equal(0, result.Start);
        Assert.Equal(3, result.End);
        Assert.False(result.HasMoreAbove);
        Assert.False(result.HasMoreBelow);
    }

    [Fact]
    public void ComputeVisibleRange_Unfocused_TopAnchoredWithMoreBelow()
    {
        // heights 3 each + 1 gap = 4 rows/card; innerHeight 10 fits 2 cards + indicator budget
        var heights = new[] { 3, 3, 3, 3, 3 };

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, null, 10);

        Assert.Equal(0, result.Start);
        Assert.False(result.HasMoreAbove);
        Assert.True(result.HasMoreBelow);
        Assert.True(result.End < heights.Length);
    }

    [Fact]
    public void ComputeVisibleRange_FocusedDeepInList_ScrollsToKeepCursorVisible()
    {
        var heights = Enumerable.Repeat(3, 10).ToArray();

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, 8, 10);

        Assert.InRange(8, result.Start, result.End - 1);
        Assert.True(result.HasMoreAbove);
    }

    [Fact]
    public void ComputeVisibleRange_FocusedAtTop_NoMoreAboveIndicator()
    {
        var heights = Enumerable.Repeat(3, 10).ToArray();

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, 0, 10);

        Assert.Equal(0, result.Start);
        Assert.False(result.HasMoreAbove);
        Assert.True(result.HasMoreBelow);
    }

    [Fact]
    public void ComputeVisibleRange_FocusedInMiddle_ShowsBothIndicators()
    {
        var heights = Enumerable.Repeat(3, 10).ToArray();

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, 5, 8);

        Assert.InRange(5, result.Start, result.End - 1);
        Assert.True(result.HasMoreAbove);
        Assert.True(result.HasMoreBelow);
    }

    [Fact]
    public void ComputeVisibleRange_SingleCardTallerThanViewport_StillShowsIt()
    {
        var heights = new[] { 30 };

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, 0, 10);

        Assert.Equal(0, result.Start);
        Assert.Equal(1, result.End);
    }

    [Fact]
    public void ComputeVisibleRange_ActiveIndexOutOfRange_Clamped()
    {
        var heights = Enumerable.Repeat(3, 5).ToArray();

        var result = ColumnScrollCalculator.ComputeVisibleRange(heights, 99, 10);

        Assert.InRange(4, result.Start, result.End - 1);
    }
}
