using HydraForge.Tui.Models;

namespace HydraForge.Tui.Tests;

public class DateFormattingTests
{
    [Fact]
    public void FormatRecentRelative_Should_Return_JustNow_Under_A_Minute()
    {
        Assert.Equal(
            "just now",
            DateFormatting.FormatRecentRelative(DateTimeOffset.UtcNow.AddSeconds(-30))
        );
    }

    [Fact]
    public void FormatRecentRelative_Should_Return_Minutes()
    {
        Assert.Equal(
            "5m ago",
            DateFormatting.FormatRecentRelative(DateTimeOffset.UtcNow.AddMinutes(-5))
        );
    }

    [Fact]
    public void FormatRecentRelative_Should_Return_Hours()
    {
        Assert.Equal(
            "3h ago",
            DateFormatting.FormatRecentRelative(DateTimeOffset.UtcNow.AddHours(-3))
        );
    }

    [Fact]
    public void FormatRecentRelative_Should_Return_Days()
    {
        Assert.Equal(
            "2d ago",
            DateFormatting.FormatRecentRelative(DateTimeOffset.UtcNow.AddDays(-2))
        );
    }

    [Fact]
    public void FormatLongRelative_Should_Return_Years()
    {
        Assert.Equal(
            "2y ago",
            DateFormatting.FormatLongRelative(DateTimeOffset.UtcNow.AddDays(-800))
        );
    }

    [Fact]
    public void FormatLongRelative_Should_Return_Months()
    {
        Assert.Equal(
            "2mo ago",
            DateFormatting.FormatLongRelative(DateTimeOffset.UtcNow.AddDays(-65))
        );
    }

    [Fact]
    public void FormatLongRelative_Should_Return_Days()
    {
        Assert.Equal(
            "5d ago",
            DateFormatting.FormatLongRelative(DateTimeOffset.UtcNow.AddDays(-5))
        );
    }

    [Fact]
    public void FormatLongRelative_Should_Return_JustNow_Under_An_Hour()
    {
        Assert.Equal(
            "just now",
            DateFormatting.FormatLongRelative(DateTimeOffset.UtcNow.AddMinutes(-30))
        );
    }

    [Fact]
    public void FormatTimestamp_Should_Use_Compact_Absolute_Format()
    {
        var dt = new DateTimeOffset(2026, 7, 30, 14, 22, 0, TimeSpan.Zero);
        Assert.Equal("Jul 30 14:22", DateFormatting.FormatTimestamp(dt));
    }
}
