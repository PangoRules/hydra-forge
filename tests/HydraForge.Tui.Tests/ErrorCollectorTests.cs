using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class ErrorCollectorTests
{
    [Fact]
    public void Add_Beyond50Entries_EvictsOldestAndCapsCountAt50()
    {
        var collector = new ErrorCollector();

        for (var i = 0; i < 51; i++)
            collector.Add($"corr-{i}", $"message-{i}");

        Assert.Equal(50, collector.Count);
        Assert.DoesNotContain(collector.GetErrors(), e => e.CorrelationId == "corr-0");
        Assert.Contains(collector.GetErrors(), e => e.CorrelationId == "corr-50");
    }

    [Fact]
    public void GetErrors_ReturnsEntriesInInsertionOrder()
    {
        var collector = new ErrorCollector();
        collector.Add("corr-1", "first");
        collector.Add("corr-2", "second");

        var errors = collector.GetErrors();

        Assert.Equal(2, errors.Count);
        Assert.Equal("corr-1", errors[0].CorrelationId);
        Assert.Equal("corr-2", errors[1].CorrelationId);
    }

    [Fact]
    public void Dismiss_ValidIndex_RemovesEntryAndDecrementsCount()
    {
        var collector = new ErrorCollector();
        collector.Add("corr-1", "first");
        collector.Add("corr-2", "second");

        collector.Dismiss(0);

        Assert.Equal(1, collector.Count);
        Assert.Equal("corr-2", collector.GetErrors()[0].CorrelationId);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Dismiss_IndexOutOfRange_IsNoOp(int index)
    {
        var collector = new ErrorCollector();
        collector.Add("corr-1", "only entry");

        collector.Dismiss(index);

        Assert.Equal(1, collector.Count);
    }

    [Fact]
    public void Dismiss_OnEmptyCollector_IsNoOp()
    {
        var collector = new ErrorCollector();

        var exception = Record.Exception(() => collector.Dismiss(0));

        Assert.Null(exception);
        Assert.Equal(0, collector.Count);
    }
}
