using HydraForge.Tui.Rendering;

namespace HydraForge.Tui.Tests;

public class ListScrollWindowTests
{
    [Fact]
    public void Compute_EmptyList_ReturnsEmptyWindow()
    {
        var result = ListScrollWindow.Compute(0, 0, 5);

        Assert.Equal(0, result.Start);
        Assert.Equal(0, result.End);
        Assert.False(result.HasMoreAbove);
        Assert.False(result.HasMoreBelow);
    }

    [Fact]
    public void Compute_AllItemsFit_NoIndicators()
    {
        var result = ListScrollWindow.Compute(3, 1, 5);

        Assert.Equal(0, result.Start);
        Assert.Equal(3, result.End);
        Assert.False(result.HasMoreAbove);
        Assert.False(result.HasMoreBelow);
    }

    [Fact]
    public void Compute_SelectionAtStart_WindowAnchoredAtZero()
    {
        var result = ListScrollWindow.Compute(20, 0, 5);

        Assert.Equal(0, result.Start);
        Assert.Equal(5, result.End);
        Assert.False(result.HasMoreAbove);
        Assert.True(result.HasMoreBelow);
    }

    [Fact]
    public void Compute_SelectionAtEnd_WindowAnchoredAtLastItems()
    {
        var result = ListScrollWindow.Compute(20, 19, 5);

        Assert.Equal(15, result.Start);
        Assert.Equal(20, result.End);
        Assert.True(result.HasMoreAbove);
        Assert.False(result.HasMoreBelow);
    }

    [Fact]
    public void Compute_SelectionInMiddle_WindowCentersSelection()
    {
        var result = ListScrollWindow.Compute(20, 10, 5);

        Assert.True(result.Start <= 10 && result.End > 10);
        Assert.Equal(5, result.End - result.Start);
        Assert.True(result.HasMoreAbove);
        Assert.True(result.HasMoreBelow);
    }

    [Fact]
    public void Compute_MaxVisibleZero_ReturnsEmptyWindow()
    {
        var result = ListScrollWindow.Compute(10, 0, 0);

        Assert.Equal(0, result.Start);
        Assert.Equal(0, result.End);
    }

    [Fact]
    public void Compute_SelectedIndexOutOfRange_Clamped()
    {
        var result = ListScrollWindow.Compute(10, 999, 5);

        Assert.Equal(5, result.Start);
        Assert.Equal(10, result.End);
    }
}
